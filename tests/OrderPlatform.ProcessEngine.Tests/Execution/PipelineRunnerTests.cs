using NSubstitute;
using OrderPlatform.ProcessEngine.Execution;
using OrderPlatform.ProcessEngine.Runs;
using OrderPlatform.ProcessEngine.Steps;
using OrderPlatform.ProcessEngine.Storage;
using OrderPlatform.ProcessEngine.Tests.Support;

namespace OrderPlatform.ProcessEngine.Tests.Execution;

/// <summary>The API runs the first steps inline (for example up to the price) and leaves the rest to the worker.</summary>
public sealed class PipelineRunnerTests : IDisposable
{
    private static readonly TimeSpan Budget = TimeSpan.FromSeconds(5);

    private readonly EngineHarness _engine = new();
    private readonly IPipelineRunStore _store = Substitute.For<IPipelineRunStore>();
    private readonly PipelineRun _run;
    private readonly List<string?> _lockAtEverySave = [];

    public PipelineRunnerTests()
    {
        _run = _engine.NewRun();
        _store.When(store => store.SaveChangesAsync(Arg.Any<CancellationToken>())).Do(_ => _lockAtEverySave.Add(_run.LockedBy));
    }

    [Fact]
    public async Task RunUntil_StepPasses_RunsOnlyUpToThatStep()
    {
        var outcome = await RunUntilAsync(nameof(StepTwo));

        Assert.Equal(InlineRunOutcome.Reached, outcome);
        Assert.Equal(["execute one", "execute two"], _engine.Journal.Entries);
        Assert.Equal(RunStatus.Running, _run.Status);
    }

    [Fact]
    public async Task RunUntil_LocksTheRunBeforeTheFirstSave_SavesAfterEveryStep_AndUnlocksAtTheEnd()
    {
        await RunUntilAsync(nameof(StepTwo));

        Assert.Equal([PipelineRunner.InlineOwner, PipelineRunner.InlineOwner, PipelineRunner.InlineOwner, null], _lockAtEverySave);
        Assert.False(_run.IsLocked(_engine.Clock.GetUtcNow()));
    }

    [Fact]
    public async Task RunUntil_LockOutlivesTheBudget_SoTheWorkerWaitsForTheLastSave()
    {
        var lockedUntil = DateTimeOffset.MinValue;
        _engine.One.ThenExecute(_ =>
        {
            lockedUntil = _run.LockedUntil!.Value;
            return StepResult.Success();
        });

        await RunUntilAsync(nameof(StepOne));

        Assert.Equal(_engine.Clock.GetUtcNow() + Budget + PipelineRunner.SaveMargin, lockedUntil);
    }

    [Fact]
    public async Task RunUntil_StepSkipped_CountsAsReached()
    {
        _engine.Two.Runs = false;

        var outcome = await RunUntilAsync(nameof(StepTwo));

        Assert.Equal(InlineRunOutcome.Reached, outcome);
        Assert.Equal(["execute one"], _engine.Journal.Entries);
    }

    [Fact]
    public async Task RunUntil_RetryScheduled_ReturnsInProgressAndLeavesTheRetryToTheWorker()
    {
        _engine.Two.ThenExecute(StepResult.Retryable("busy"));

        var outcome = await RunUntilAsync(nameof(StepTwo));

        Assert.Equal(InlineRunOutcome.InProgress, outcome);
        Assert.True(_run.NextAttemptAt > _engine.Clock.GetUtcNow());
        Assert.Null(_run.LockedBy);
    }

    [Fact]
    public async Task RunUntil_StepFailsFatally_ReturnsFailedAndDoesNotUndoInline()
    {
        _engine.Two.ThenExecute(StepResult.Fatal("out of stock"));

        var outcome = await RunUntilAsync(nameof(StepThree));

        Assert.Equal(InlineRunOutcome.Failed, outcome);
        Assert.Equal(RunStatus.Compensating, _run.Status);
        Assert.DoesNotContain(_engine.Journal.Entries, entry => entry.StartsWith("compensate", StringComparison.Ordinal));
    }

    [Fact]
    public async Task RunUntil_StepWaits_ReturnsInProgress()
    {
        _engine.One.ThenExecute(StepResult.Waiting());

        Assert.Equal(InlineRunOutcome.InProgress, await RunUntilAsync(nameof(StepTwo)));
    }

    [Fact]
    public async Task RunUntil_BudgetRunsOutBetweenSteps_StopsAndReturnsInProgress()
    {
        _engine.One.ThenExecute(_ =>
        {
            _engine.Clock.Advance(Budget);
            return StepResult.Success();
        });

        var outcome = await RunUntilAsync(nameof(StepThree));

        Assert.Equal(InlineRunOutcome.InProgress, outcome);
        Assert.Equal(["execute one"], _engine.Journal.Entries);
        Assert.Equal(1, _run.NextStepIndex);
    }

    [Fact]
    public async Task RunUntil_BudgetRunsOutDuringAStep_InterruptsTheStepWithoutUsingARetry()
    {
        _engine.One.ThenExecute(token =>
        {
            _engine.Clock.Advance(Budget);
            token.ThrowIfCancellationRequested();
            return StepResult.Success();
        });

        var outcome = await RunUntilAsync(nameof(StepOne));

        var execution = _run.FindExecution(0, StepExecutionKind.Execute)!;
        Assert.Equal(InlineRunOutcome.InProgress, outcome);
        Assert.Equal(StepOutcome.Interrupted, execution.Attempts.Single().Outcome);
        Assert.Equal(0, execution.AttemptCount);
        Assert.True(_run.IsDue(_engine.Clock.GetUtcNow()));
    }

    [Fact]
    public async Task RunCancellation_UndoesTheSucceededStepsInReverseOrder_AndReleasesTheLock()
    {
        await RunUntilAsync(nameof(StepTwo));
        _run.RequestCancel(_engine.Clock.GetUtcNow());

        var outcome = await CancelAsync();

        Assert.Equal(InlineRunOutcome.Reached, outcome);
        Assert.Equal(RunStatus.Compensated, _run.Status);
        Assert.Equal(CompensationCause.Cancelled, _run.CompensationCause);
        Assert.Equal(["execute one", "execute two", "compensate two", "compensate one"], _engine.Journal.Entries);
        Assert.Null(_run.LockedBy);
        Assert.Null(_lockAtEverySave[^1]);
    }

    [Fact]
    public async Task RunCancellation_NothingRanYet_IsCompensatedWithoutCallingAnyStep()
    {
        _run.RequestCancel(_engine.Clock.GetUtcNow());

        var outcome = await CancelAsync();

        Assert.Equal(InlineRunOutcome.Reached, outcome);
        Assert.Equal(RunStatus.Compensated, _run.Status);
        Assert.Empty(_engine.Journal.Entries);
    }

    [Fact]
    public async Task RunCancellation_UndoHasToBeRetried_ReturnsInProgressAndLeavesTheRetryScheduled()
    {
        await RunUntilAsync(nameof(StepTwo));
        _engine.Two.ThenCompensate(StepResult.Retryable("inventory down"));
        _run.RequestCancel(_engine.Clock.GetUtcNow());

        var outcome = await CancelAsync();

        Assert.Equal(InlineRunOutcome.InProgress, outcome);
        Assert.Equal(RunStatus.Compensating, _run.Status);
        Assert.True(_run.NextAttemptAt > _engine.Clock.GetUtcNow());
        Assert.Equal(0, _engine.One.CompensateCalls);
        Assert.Null(_run.LockedBy);
    }

    [Fact]
    public async Task RunCancellation_UndoFailsForGood_ReturnsFailed()
    {
        await RunUntilAsync(nameof(StepOne));
        _engine.One.ThenCompensate(StepResult.Fatal("reservation unknown"));
        _run.RequestCancel(_engine.Clock.GetUtcNow());

        var outcome = await CancelAsync();

        Assert.Equal(InlineRunOutcome.Failed, outcome);
        Assert.Equal(RunStatus.RequiresAttention, _run.Status);
    }

    [Fact]
    public async Task RunCancellation_RunHeldBySomeoneElse_SavesTheRequestAndRunsNothing()
    {
        _run.Lock("worker-1", _engine.Clock.GetUtcNow().AddMinutes(1));
        _run.RequestCancel(_engine.Clock.GetUtcNow());

        var outcome = await CancelAsync();

        Assert.Equal(InlineRunOutcome.InProgress, outcome);
        Assert.Equal("worker-1", _run.LockedBy);
        Assert.Equal(RunStatus.Running, _run.Status);
        await _store.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RunCancellation_WithoutACancelRequest_ThrowsInsteadOfRunningThePipelineForward()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(CancelAsync);

        Assert.Empty(_engine.Journal.Entries);
    }

    [Fact]
    public async Task RunUntil_StepNotInThePipeline_Throws() =>
        await Assert.ThrowsAsync<InvalidOperationException>(() => RunUntilAsync("NoSuchStep"));

    [Fact]
    public async Task RunUntil_PipelineNotRegistered_Throws()
    {
        var run = PipelineRun.Start("other", 1, Guid.CreateVersion7(), "{}", _engine.Clock.GetUtcNow());

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _engine.Runner(_store).RunUntilAsync(run, nameof(StepOne), Budget, CancellationToken.None));
    }

    [Fact]
    public async Task RunUntil_ZeroBudget_Throws() =>
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            _engine.Runner(_store).RunUntilAsync(_run, nameof(StepOne), TimeSpan.Zero, CancellationToken.None));

    public void Dispose() => _engine.Dispose();

    private Task<InlineRunOutcome> CancelAsync() =>
        _engine.Runner(_store).RunCancellationAsync(_run, Budget, CancellationToken.None);

    private Task<InlineRunOutcome> RunUntilAsync(string stepKey) =>
        _engine.Runner(_store).RunUntilAsync(_run, stepKey, Budget, CancellationToken.None);
}
