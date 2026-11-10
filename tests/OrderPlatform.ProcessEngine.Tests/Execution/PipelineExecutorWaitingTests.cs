using OrderPlatform.ProcessEngine.Pipelines;
using OrderPlatform.ProcessEngine.Runs;
using OrderPlatform.ProcessEngine.Steps;
using OrderPlatform.ProcessEngine.Tests.Support;

namespace OrderPlatform.ProcessEngine.Tests.Execution;

/// <summary>An OnDemand step after the pivot, like AwaitDelivery: it waits until it is triggered, for at most one day.</summary>
public sealed class PipelineExecutorWaitingTests : IDisposable
{
    private static readonly TimeSpan Timeout = TimeSpan.FromDays(1);

    private readonly EngineHarness _engine = new(new TestPipeline(
        1,
        TestPipeline.Step<StepOne>(),
        TestPipeline.Step<StepThree>(StepKind.Pivot),
        TestPipeline.Step<StepFour>(StepKind.ForwardOnly, TriggerMode.OnDemand, timeout: Timeout)));

    [Fact]
    public async Task Advance_StepWaits_RunWaitsUntilTheTimeout()
    {
        _engine.Four.ThenExecute(StepResult.Waiting());
        var now = _engine.Clock.GetUtcNow();

        var run = await _engine.AdvanceWhileDueAsync(_engine.NewRun());

        var execution = run.FindExecution(2, StepExecutionKind.Execute)!;
        Assert.Equal(RunStatus.Waiting, run.Status);
        Assert.Equal(now, run.WaitingSince);
        Assert.Equal(now + Timeout, run.NextAttemptAt);
        Assert.Equal(StepExecutionStatus.Waiting, execution.Status);
        Assert.Equal(0, execution.AttemptCount);
    }

    [Fact]
    public async Task Wake_WaitingRun_RunsTheStepAgainAndContinues()
    {
        _engine.Four.ThenExecute(StepResult.Waiting());
        var run = await _engine.AdvanceWhileDueAsync(_engine.NewRun());

        Assert.True(run.Wake(_engine.Clock.GetUtcNow()));
        await _engine.AdvanceWhileDueAsync(run);

        Assert.Equal(RunStatus.Completed, run.Status);
        Assert.Null(run.WaitingSince);
        Assert.Equal(2, _engine.Four.ExecuteCalls);
    }

    [Fact]
    public async Task Advance_StillWaitingAtTheTimeout_RequiresAttention()
    {
        _engine.Four.ThenExecute(StepResult.Waiting(), StepResult.Waiting());
        var run = await _engine.AdvanceWhileDueAsync(_engine.NewRun());

        _engine.Clock.Advance(Timeout);
        await _engine.AdvanceWhileDueAsync(run);

        Assert.Equal(RunStatus.RequiresAttention, run.Status);
        Assert.Contains("StepFour", run.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Advance_WaitingBeforeTheTimeout_IsNotDue()
    {
        _engine.Four.ThenExecute(StepResult.Waiting());
        var run = await _engine.AdvanceWhileDueAsync(_engine.NewRun());

        _engine.Clock.Advance(Timeout - TimeSpan.FromMinutes(1));

        Assert.False(await _engine.AdvanceAsync(run));
    }

    [Fact]
    public async Task Advance_CancelWhileWaitingBeforeThePivot_UndoesWhatRan()
    {
        using var engine = new EngineHarness(new TestPipeline(
            1,
            TestPipeline.Step<StepOne>(),
            TestPipeline.Step<StepTwo>(trigger: TriggerMode.OnDemand),
            TestPipeline.Step<StepThree>(StepKind.Pivot)));
        engine.Two.ThenExecute(StepResult.Waiting());
        var run = await engine.AdvanceWhileDueAsync(engine.NewRun());
        Assert.Equal(RunStatus.Waiting, run.Status);
        Assert.Null(run.NextAttemptAt);

        Assert.True(run.RequestCancel(engine.Clock.GetUtcNow()));
        await engine.AdvanceWhileDueAsync(run);

        Assert.Equal(RunStatus.Compensated, run.Status);
        Assert.Equal(["execute one", "execute two", "compensate one"], engine.Journal.Entries);
    }

    public void Dispose() => _engine.Dispose();
}
