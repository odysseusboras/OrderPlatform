using OrderPlatform.ProcessEngine.Pipelines;
using OrderPlatform.ProcessEngine.Runs;
using OrderPlatform.ProcessEngine.Steps;
using OrderPlatform.ProcessEngine.Tests.Support;

namespace OrderPlatform.ProcessEngine.Tests.Execution;

public sealed class PipelineExecutorForwardTests : IDisposable
{
    private readonly EngineHarness _engine = new();

    [Fact]
    public async Task Advance_EveryStepSucceeds_RunsTheStepsInOrderAndCompletes()
    {
        var run = await _engine.AdvanceWhileDueAsync(_engine.NewRun());

        Assert.Equal(RunStatus.Completed, run.Status);
        Assert.Null(run.NextAttemptAt);
        Assert.Equal(["execute one", "execute two", "execute three", "execute four", "execute five"], _engine.Journal.Entries);
        Assert.All(run.Steps, step => Assert.Equal(StepExecutionStatus.Succeeded, step.Status));
    }

    [Fact]
    public async Task Advance_RunsOneStepPerCall()
    {
        var run = _engine.NewRun();

        await _engine.AdvanceAsync(run);

        Assert.Equal(1, run.NextStepIndex);
        Assert.Equal(RunStatus.Running, run.Status);
        Assert.Equal(["execute one"], _engine.Journal.Entries);
    }

    [Fact]
    public async Task Advance_StoresTheContextTheStepsChanged()
    {
        var run = await _engine.AdvanceWhileDueAsync(_engine.NewRun());

        Assert.Equal(["one", "two", "three", "four", "five"], EngineHarness.ContextOf(run).Visited);
    }

    [Fact]
    public async Task Advance_RunIsFinished_ReturnsFalseAndRunsNothing()
    {
        var run = await _engine.AdvanceWhileDueAsync(_engine.NewRun());
        _engine.Journal.Entries.Clear();

        var advanced = await _engine.AdvanceAsync(run);

        Assert.False(advanced);
        Assert.Empty(_engine.Journal.Entries);
    }

    [Fact]
    public async Task Advance_ShouldRunIsFalse_SkipsTheStepAsOneMoveWithoutRunningIt()
    {
        _engine.Two.Runs = false;
        var run = _engine.NewRun();
        await _engine.AdvanceAsync(run);

        await _engine.AdvanceAsync(run);

        Assert.Equal(["execute one"], _engine.Journal.Entries);
        Assert.Equal(StepExecutionStatus.Skipped, run.FindExecution(1, StepExecutionKind.Execute)!.Status);
        Assert.Equal(2, run.NextStepIndex);
        Assert.True(run.IsDue(_engine.Clock.GetUtcNow()));
    }

    [Fact]
    public async Task Advance_AfterTheLastStep_CompletesAsItsOwnMove()
    {
        var run = _engine.NewRun();
        for (var step = 0; step < 5; step++)
        {
            await _engine.AdvanceAsync(run);
        }

        Assert.Equal(RunStatus.Running, run.Status);
        await _engine.AdvanceAsync(run);

        Assert.Equal(RunStatus.Completed, run.Status);
    }

    [Fact]
    public async Task Advance_PivotSucceeds_MarksThePivotAsPassed()
    {
        var run = _engine.NewRun();
        await _engine.AdvanceAsync(run);
        await _engine.AdvanceAsync(run);
        Assert.False(run.PivotPassed);

        await _engine.AdvanceAsync(run);

        Assert.True(run.PivotPassed);
    }

    [Fact]
    public async Task Advance_EveryAttempt_IsRecordedWithItsOutcome()
    {
        _engine.One.ThenExecute(StepResult.Retryable("timeout"));
        var run = _engine.NewRun();
        await _engine.AdvanceAsync(run);
        _engine.Clock.Advance(TimeSpan.FromSeconds(1));

        await _engine.AdvanceAsync(run);

        var attempts = run.FindExecution(0, StepExecutionKind.Execute)!.Attempts;
        Assert.Equal([1, 2], attempts.Select(attempt => attempt.Number));
        Assert.Equal([StepOutcome.RetryableError, StepOutcome.Success], attempts.Select(attempt => attempt.Outcome));
        Assert.Equal("timeout", attempts[0].Error);
    }

    [Fact]
    public async Task Advance_SecondVersionRegistered_RunFollowsTheVersionItWasStartedWith()
    {
        var versionTwo = new TestPipeline(2, TestPipeline.Step<StepOne>(), TestPipeline.Step<StepFour>(StepKind.ForwardOnly));
        using var engine = new EngineHarness(TestPipeline.Standard(), versionTwo);

        var oldRun = await engine.AdvanceWhileDueAsync(engine.NewRun(version: 1));
        var newRun = await engine.AdvanceWhileDueAsync(engine.NewRun(version: 2));

        Assert.Equal(5, oldRun.Steps.Count);
        Assert.Equal(2, newRun.Steps.Count);
        Assert.Equal(RunStatus.Completed, newRun.Status);
    }

    [Fact]
    public async Task Advance_VersionNotRegistered_Throws()
    {
        var run = _engine.NewRun(version: 9);

        await Assert.ThrowsAsync<InvalidOperationException>(() => _engine.AdvanceAsync(run));
    }

    public void Dispose() => _engine.Dispose();
}
