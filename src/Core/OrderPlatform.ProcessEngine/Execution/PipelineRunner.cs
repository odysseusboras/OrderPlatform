using OrderPlatform.ProcessEngine.Runs;
using OrderPlatform.ProcessEngine.Storage;

namespace OrderPlatform.ProcessEngine.Execution;

internal sealed class PipelineRunner(IEnumerable<IPipelineExecutor> executors, IPipelineRunStore store, TimeProvider timeProvider) : IPipelineRunner
{
    internal const string InlineOwner = "inline";

    /// <summary>Extra lock time after the budget, so the worker cannot take the run while the last save is still running.</summary>
    internal static readonly TimeSpan SaveMargin = TimeSpan.FromSeconds(30);

    public async Task<InlineRunOutcome> RunUntilAsync(PipelineRun run, string stepKey, TimeSpan budget, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(run);
        ArgumentException.ThrowIfNullOrWhiteSpace(stepKey);

        return await RunLockedAsync(run, budget, cancellationToken, (executor, stepToken) =>
            AdvanceUntilAsync(run, stepKey, executor, stepToken, cancellationToken));
    }

    public async Task<InlineRunOutcome> RunCancellationAsync(PipelineRun run, TimeSpan budget, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(run);
        if (run.CancelRequestedAt is null)
        {
            // Without a cancel request the loop below would run the pipeline forward to its end.
            throw new InvalidOperationException($"Run {run.Id} has no cancel request; call RequestCancel first.");
        }

        if (run.IsLocked(timeProvider.GetUtcNow()))
        {
            // Someone else is running it right now: keep the request; they undo the steps on their next move.
            await store.SaveChangesAsync(cancellationToken);
            return InlineRunOutcome.InProgress;
        }

        return await RunLockedAsync(run, budget, cancellationToken, (executor, stepToken) =>
            UndoAsync(run, executor, stepToken, cancellationToken));
    }

    private async Task<InlineRunOutcome> RunLockedAsync(
        PipelineRun run, TimeSpan budget, CancellationToken cancellationToken, Func<IPipelineExecutor, CancellationToken, Task<InlineRunOutcome>> advance)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(budget, TimeSpan.Zero);
        var executor = executors.SingleOrDefault(candidate => candidate.PipelineName == run.PipelineName)
            ?? throw new InvalidOperationException($"No pipeline named {run.PipelineName} is registered.");

        run.Lock(InlineOwner, timeProvider.GetUtcNow() + budget + SaveMargin);
        await store.SaveChangesAsync(cancellationToken);

        // Steps stop when the budget runs out or the client goes away; saves only stop when the client goes away.
        using var timeout = new CancellationTokenSource(budget, timeProvider);
        using var stepCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);
        var outcome = await advance(executor, stepCancellation.Token);

        run.Unlock();
        await store.SaveChangesAsync(cancellationToken);
        return outcome;
    }

    /// <summary>Undoes the succeeded steps in reverse order, saving after each, until nothing is left or an undo has to wait.</summary>
    private async Task<InlineRunOutcome> UndoAsync(PipelineRun run, IPipelineExecutor executor, CancellationToken stepToken, CancellationToken saveToken)
    {
        while (run.Status is not (RunStatus.Compensated or RunStatus.RequiresAttention))
        {
            if (stepToken.IsCancellationRequested || !await executor.AdvanceAsync(run, stepToken))
            {
                return InlineRunOutcome.InProgress;
            }

            await store.SaveChangesAsync(saveToken);
        }

        return run.Status == RunStatus.Compensated ? InlineRunOutcome.Reached : InlineRunOutcome.Failed;
    }

    private async Task<InlineRunOutcome> AdvanceUntilAsync(
        PipelineRun run, string stepKey, IPipelineExecutor executor, CancellationToken stepToken, CancellationToken saveToken)
    {
        while (true)
        {
            if (run.HasPassed(stepKey))
            {
                return InlineRunOutcome.Reached;
            }

            if (run.Status != RunStatus.Running)
            {
                return OutcomeOf(run, stepKey);
            }

            if (stepToken.IsCancellationRequested || !await executor.AdvanceAsync(run, stepToken))
            {
                return InlineRunOutcome.InProgress;
            }

            await store.SaveChangesAsync(saveToken);
        }
    }

    private static InlineRunOutcome OutcomeOf(PipelineRun run, string stepKey) => run.Status switch
    {
        RunStatus.Waiting => InlineRunOutcome.InProgress,
        RunStatus.Compensating or RunStatus.Compensated or RunStatus.RequiresAttention => InlineRunOutcome.Failed,
        _ => throw new InvalidOperationException($"Run {run.Id} finished without passing step {stepKey}; the step is not in {run.PipelineName} v{run.PipelineVersion}."),
    };
}
