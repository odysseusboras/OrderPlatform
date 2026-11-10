using OrderPlatform.ProcessEngine.Runs;
using OrderPlatform.ProcessEngine.Steps;
using OrderPlatform.ProcessEngine.Tests.Support;

namespace OrderPlatform.ProcessEngine.Tests.Execution;

public sealed class PipelineExecutorCompensationTests : IDisposable
{
    private readonly EngineHarness _engine = new();

    [Fact]
    public async Task Advance_PivotFailsFatally_UndoesTheSucceededStepsInReverseOrder()
    {
        _engine.Three.ThenExecute(StepResult.Fatal("card declined"));

        var run = await _engine.AdvanceWhileDueAsync(_engine.NewRun());

        Assert.Equal(RunStatus.Compensated, run.Status);
        Assert.Equal(CompensationCause.StepFailed, run.CompensationCause);
        Assert.Contains("card declined", run.Reason, StringComparison.Ordinal);
        Assert.Equal(
            ["execute one", "execute two", "execute three", "compensate two", "compensate one"],
            _engine.Journal.Entries);
    }

    [Fact]
    public async Task Advance_FatalError_DoesNotUndoTheStepThatFailed()
    {
        _engine.Two.ThenExecute(StepResult.Fatal("out of stock"));

        var run = await _engine.AdvanceWhileDueAsync(_engine.NewRun());

        Assert.Equal(RunStatus.Compensated, run.Status);
        Assert.Equal(0, _engine.Two.CompensateCalls);
        Assert.Equal(1, _engine.One.CompensateCalls);
    }

    [Fact]
    public async Task Advance_FatalErrorOnTheFirstStep_IsCompensatedWithNothingToUndo()
    {
        _engine.One.ThenExecute(StepResult.Fatal("invalid"));

        var run = await _engine.AdvanceWhileDueAsync(_engine.NewRun());

        Assert.Equal(RunStatus.Compensated, run.Status);
        Assert.Equal(["execute one"], _engine.Journal.Entries);
    }

    [Fact]
    public async Task Advance_SkippedStep_IsNeverUndone()
    {
        _engine.Two.Runs = false;
        _engine.Three.ThenExecute(StepResult.Fatal("declined"));

        await _engine.AdvanceWhileDueAsync(_engine.NewRun());

        Assert.Equal(0, _engine.Two.CompensateCalls);
        Assert.Equal(1, _engine.One.CompensateCalls);
    }

    [Fact]
    public async Task Advance_FatalErrorAfterThePivot_RequiresAttentionAndUndoesNothing()
    {
        _engine.Four.ThenExecute(StepResult.Fatal("carrier rejected"));

        var run = await _engine.AdvanceWhileDueAsync(_engine.NewRun());

        Assert.Equal(RunStatus.RequiresAttention, run.Status);
        Assert.True(run.PivotPassed);
        Assert.Contains("carrier rejected", run.Reason, StringComparison.Ordinal);
        Assert.DoesNotContain(_engine.Journal.Entries, entry => entry.StartsWith("compensate", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Advance_UndoFailsRetryably_RetriesTheUndoAfterTheDelay()
    {
        _engine.Three.ThenExecute(StepResult.Fatal("declined"));
        _engine.Two.ThenCompensate(StepResult.Retryable("inventory down"));
        var run = await _engine.AdvanceWhileDueAsync(_engine.NewRun());
        Assert.Equal(RunStatus.Compensating, run.Status);
        Assert.Equal(_engine.Clock.GetUtcNow().AddSeconds(1), run.NextAttemptAt);

        _engine.Clock.Advance(TimeSpan.FromSeconds(1));
        await _engine.AdvanceWhileDueAsync(run);

        Assert.Equal(RunStatus.Compensated, run.Status);
        Assert.Equal(2, _engine.Two.CompensateCalls);
        Assert.Equal(1, _engine.One.CompensateCalls);
    }

    [Fact]
    public async Task Advance_UndoFailsFatally_RequiresAttentionAndStopsUndoing()
    {
        _engine.Three.ThenExecute(StepResult.Fatal("declined"));
        _engine.Two.ThenCompensate(StepResult.Fatal("reservation unknown"));

        var run = await _engine.AdvanceWhileDueAsync(_engine.NewRun());

        Assert.Equal(RunStatus.RequiresAttention, run.Status);
        Assert.Contains("StepTwo", run.Reason, StringComparison.Ordinal);
        Assert.Equal(0, _engine.One.CompensateCalls);
        Assert.Equal(StepExecutionStatus.Failed, run.FindExecution(1, StepExecutionKind.Compensate)!.Status);
    }

    [Fact]
    public async Task Advance_CancelRequestedBeforeThePivot_UndoesWhatRanAndStopsMovingForward()
    {
        var run = _engine.NewRun();
        await _engine.AdvanceAsync(run);

        Assert.True(run.RequestCancel(_engine.Clock.GetUtcNow()));
        await _engine.AdvanceWhileDueAsync(run);

        Assert.Equal(RunStatus.Compensated, run.Status);
        Assert.Equal(CompensationCause.Cancelled, run.CompensationCause);
        Assert.Equal(["execute one", "compensate one"], _engine.Journal.Entries);
    }

    [Fact]
    public async Task Advance_CancelRequestedAfterThePivot_IsRefusedAndTheRunCompletes()
    {
        var run = _engine.NewRun();
        for (var step = 0; step < 3; step++)
        {
            await _engine.AdvanceAsync(run);
        }

        var accepted = run.RequestCancel(_engine.Clock.GetUtcNow());
        await _engine.AdvanceWhileDueAsync(run);

        Assert.False(accepted);
        Assert.Null(run.CancelRequestedAt);
        Assert.Equal(RunStatus.Completed, run.Status);
    }

    public void Dispose() => _engine.Dispose();
}
