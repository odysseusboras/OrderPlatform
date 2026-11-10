using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrderPlatform.ProcessEngine.Pipelines;
using OrderPlatform.ProcessEngine.Runs;
using OrderPlatform.ProcessEngine.Steps;

namespace OrderPlatform.ProcessEngine.Execution;

/// <summary>
/// The saga orchestrator for one context type. Decides what runs next, records every attempt,
/// retries with backoff, and undoes succeeded steps in reverse order when a step before the pivot fails or the run is cancelled.
/// </summary>
internal sealed class PipelineExecutor<TContext>(
    IEnumerable<PipelineDefinition<TContext>> definitions,
    IEnumerable<IPipelineObserver<TContext>> observers,
    IServiceProvider services,
    TimeProvider timeProvider,
    ILogger<PipelineExecutor<TContext>> logger) : IPipelineExecutor
    where TContext : class
{
    private readonly Dictionary<int, PipelineDefinition<TContext>> _versions = definitions.ToDictionary(definition => definition.Version);

    public string PipelineName => _versions.Values.First().Name;

    public async Task<bool> AdvanceAsync(PipelineRun run, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(run);
        if (!run.IsDue(timeProvider.GetUtcNow()))
        {
            return false;
        }

        var definition = DefinitionFor(run);
        var context = PipelineContextSerializer.Deserialize<TContext>(run.ContextJson);

        if (run.Direction == RunDirection.Forward && run.CancelRequestedAt is not null && !run.PivotPassed)
        {
            StartCompensating(run, CompensationCause.Cancelled, "Cancellation requested.");
        }

        if (run.Direction == RunDirection.Forward)
        {
            await MoveForwardAsync(run, definition, context, cancellationToken);
        }
        else
        {
            await MoveBackwardAsync(run, definition, context, cancellationToken);
        }

        // The move already happened; recording its effects must not be cut short by a shutdown or a time budget.
        foreach (var observer in observers)
        {
            await observer.OnAdvancedAsync(run, context, CancellationToken.None);
        }

        run.UpdateContext(PipelineContextSerializer.Serialize(context));
        return true;
    }

    private PipelineDefinition<TContext> DefinitionFor(PipelineRun run) =>
        _versions.TryGetValue(run.PipelineVersion, out var definition)
            ? definition
            : throw new InvalidOperationException(
                $"Run {run.Id} needs {run.PipelineName} v{run.PipelineVersion}, which is not registered. Keep old versions until their runs finish.");

    private async Task MoveForwardAsync(PipelineRun run, PipelineDefinition<TContext> definition, TContext context, CancellationToken cancellationToken)
    {
        if (run.NextStepIndex >= definition.Steps.Count)
        {
            run.Complete();
            return;
        }

        var stepDefinition = definition.Steps[run.NextStepIndex];
        var step = ResolveStep(stepDefinition);
        var execution = run.ExecutionFor(stepDefinition.Key, run.NextStepIndex, StepExecutionKind.Execute);

        if (!step.ShouldRun(context))
        {
            var now = timeProvider.GetUtcNow();
            execution.Skip(now);
            run.MoveForward(now);
            return;
        }

        var result = await RunAsync(run, execution, () => step.ExecuteAsync(context, cancellationToken), cancellationToken);
        HandleForwardResult(run, stepDefinition, execution, result);
    }

    private void HandleForwardResult(PipelineRun run, PipelineStepDefinition stepDefinition, StepExecution execution, StepResult result)
    {
        var now = timeProvider.GetUtcNow();
        switch (result.Outcome)
        {
            case StepOutcome.Success:
                execution.Succeed(now);
                if (stepDefinition.Kind == StepKind.Pivot)
                {
                    run.PassPivot();
                }

                run.MoveForward(now);
                break;

            case StepOutcome.Waiting:
                execution.MarkWaiting();
                run.Wait(now, stepDefinition.Timeout is { } timeout ? (run.WaitingSince ?? now) + timeout : null);
                if (stepDefinition.Timeout is { } limit && now >= run.WaitingSince!.Value + limit)
                {
                    RequireAttention(run, $"Step {stepDefinition.Key} was not triggered within {limit}.");
                }

                break;

            case StepOutcome.Interrupted:
                run.ScheduleAt(now);
                break;

            case StepOutcome.RetryableError when execution.AttemptCount < stepDefinition.Retry.MaxAttempts:
                run.ScheduleAt(now + RetrySchedule.DelayAfter(stepDefinition.Retry, execution.AttemptCount));
                break;

            case StepOutcome.RetryableError:
                execution.Fail(now);
                RequireAttention(run, $"Step {stepDefinition.Key} failed {execution.AttemptCount} times: {result.Error}");
                break;

            case StepOutcome.FatalError when run.PivotPassed || stepDefinition.Kind == StepKind.ForwardOnly:
                execution.Fail(now);
                RequireAttention(run, $"Step {stepDefinition.Key} failed after the pivot, nothing can be undone automatically: {result.Error}");
                break;

            case StepOutcome.FatalError:
                execution.Fail(now);
                StartCompensating(run, CompensationCause.StepFailed, $"Step {stepDefinition.Key} failed: {result.Error}");
                break;

            default:
                throw new InvalidOperationException($"Unknown step outcome {result.Outcome}.");
        }
    }

    private async Task MoveBackwardAsync(PipelineRun run, PipelineDefinition<TContext> definition, TContext context, CancellationToken cancellationToken)
    {
        while (run.NextStepIndex >= 0 && !NeedsUndo(run, definition.Steps[run.NextStepIndex]))
        {
            run.MoveBackward(timeProvider.GetUtcNow());
        }

        if (run.NextStepIndex < 0)
        {
            run.FinishCompensating();
            return;
        }

        var stepDefinition = definition.Steps[run.NextStepIndex];
        var step = ResolveStep(stepDefinition);
        var execution = run.ExecutionFor(stepDefinition.Key, run.NextStepIndex, StepExecutionKind.Compensate);
        var result = await RunAsync(run, execution, () => step.CompensateAsync(context, cancellationToken), cancellationToken);
        var now = timeProvider.GetUtcNow();

        switch (result.Outcome)
        {
            case StepOutcome.Success:
                execution.Succeed(now);
                run.MoveBackward(now);
                break;

            case StepOutcome.Interrupted:
                run.ScheduleAt(now);
                break;

            case StepOutcome.RetryableError when execution.AttemptCount < stepDefinition.Retry.MaxAttempts:
                run.ScheduleAt(now + RetrySchedule.DelayAfter(stepDefinition.Retry, execution.AttemptCount));
                break;

            default:
                execution.Fail(now);
                RequireAttention(run, $"Undoing step {stepDefinition.Key} failed: {result.Error}");
                break;
        }
    }

    /// <summary>A step needs undoing when it is compensatable, its execution succeeded and its undo has not succeeded yet.</summary>
    private static bool NeedsUndo(PipelineRun run, PipelineStepDefinition stepDefinition)
    {
        var index = run.NextStepIndex;
        return stepDefinition.Kind == StepKind.Compensatable
            && run.FindExecution(index, StepExecutionKind.Execute)?.Status == StepExecutionStatus.Succeeded
            && run.FindExecution(index, StepExecutionKind.Compensate)?.Status != StepExecutionStatus.Succeeded;
    }

    private async Task<StepResult> RunAsync(PipelineRun run, StepExecution execution, Func<Task<StepResult>> action, CancellationToken cancellationToken)
    {
        var startedAt = timeProvider.GetUtcNow();
        var started = timeProvider.GetTimestamp();
        StepResult result;

        try
        {
            result = await action();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            result = StepResult.Interrupted();
        }
        catch (Exception exception)
        {
            PipelineLog.StepThrew(logger, exception, run.Id, execution.StepKey);
            result = StepResult.Retryable($"{exception.GetType().Name}: {exception.Message}");
        }

        execution.RecordAttempt(result.Outcome, result.Error, startedAt, timeProvider.GetElapsedTime(started));
        PipelineLog.StepFinished(logger, run.Id, execution.StepKey, execution.Kind.ToString(), result.Outcome);
        return result;
    }

    private IPipelineStep<TContext> ResolveStep(PipelineStepDefinition stepDefinition) =>
        (IPipelineStep<TContext>)services.GetRequiredService(stepDefinition.StepType);

    private void StartCompensating(PipelineRun run, CompensationCause cause, string reason)
    {
        run.StartCompensating(cause, reason, timeProvider.GetUtcNow());
        PipelineLog.CompensationStarted(logger, run.Id, cause.ToString(), reason);
    }

    private void RequireAttention(PipelineRun run, string reason)
    {
        run.RequireAttention(reason);
        PipelineLog.RequiresAttention(logger, run.Id, reason);
    }
}
