using OrderPlatform.ProcessEngine.Execution;
using OrderPlatform.ProcessEngine.Runs;
using OrderPlatform.ProcessEngine.Steps;
using OrderPlatform.ProcessEngine.Tests.Support;

namespace OrderPlatform.ProcessEngine.Tests.Runs;

public sealed class PipelineRunTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 10, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Start_IsRunningForwardFromTheFirstStep_AndDueNow()
    {
        var run = NewRun();

        Assert.Equal(RunStatus.Running, run.Status);
        Assert.Equal(RunDirection.Forward, run.Direction);
        Assert.Equal(0, run.NextStepIndex);
        Assert.True(run.IsDue(Now));
        Assert.False(run.IsDue(Now.AddTicks(-1)));
    }

    [Fact]
    public void RequestCancel_BeforeThePivot_IsAcceptedAndMakesTheRunDue()
    {
        var run = NewRun();
        run.ScheduleAt(Now.AddHours(1));

        Assert.True(run.RequestCancel(Now));
        Assert.Equal(Now, run.CancelRequestedAt);
        Assert.True(run.IsDue(Now));
    }

    [Fact]
    public void RequestCancel_AfterThePivot_IsRefused()
    {
        var run = NewRun();
        run.PassPivot();

        Assert.False(run.CanCancel);
        Assert.False(run.RequestCancel(Now));
        Assert.Null(run.CancelRequestedAt);
    }

    [Theory]
    [InlineData(RunStatus.Compensating)]
    [InlineData(RunStatus.Completed)]
    [InlineData(RunStatus.Compensated)]
    [InlineData(RunStatus.RequiresAttention)]
    public void RequestCancel_WhenNotMovingForward_IsRefused(RunStatus status) =>
        Assert.False(RunIn(status).RequestCancel(Now));

    [Fact]
    public void Wake_WhenNotWaiting_IsRefused() =>
        Assert.False(NewRun().Wake(Now));

    [Fact]
    public void Wake_WaitingRun_RunsAgainNow()
    {
        var run = NewRun();
        run.Wait(Now, deadline: null);
        Assert.False(run.IsDue(Now.AddYears(1)));

        Assert.True(run.Wake(Now));
        Assert.Equal(RunStatus.Running, run.Status);
        Assert.True(run.IsDue(Now));
    }

    [Theory]
    [InlineData(RunStatus.Completed)]
    [InlineData(RunStatus.Compensated)]
    [InlineData(RunStatus.RequiresAttention)]
    public void IsDue_StoppedRun_IsNeverDue(RunStatus status) =>
        Assert.False(RunIn(status).IsDue(Now.AddYears(1)));

    [Fact]
    public void ExecutionFor_SameStepTwice_ReturnsTheSameExecution()
    {
        var run = NewRun();

        var first = run.ExecutionFor("one", 0, StepExecutionKind.Execute);
        var second = run.ExecutionFor("one", 0, StepExecutionKind.Execute);
        var undo = run.ExecutionFor("one", 0, StepExecutionKind.Compensate);

        Assert.Same(first, second);
        Assert.NotSame(first, undo);
        Assert.Equal(2, run.Steps.Count);
        Assert.Equal(run.Id, first.RunId);
    }

    [Theory]
    [InlineData(StepOutcome.Success, 1)]
    [InlineData(StepOutcome.RetryableError, 1)]
    [InlineData(StepOutcome.FatalError, 1)]
    [InlineData(StepOutcome.Waiting, 0)]
    [InlineData(StepOutcome.Interrupted, 0)]
    public void RecordAttempt_CountsOnlyRealAttempts(StepOutcome outcome, int expectedCount)
    {
        var execution = NewRun().ExecutionFor("one", 0, StepExecutionKind.Execute);

        execution.RecordAttempt(outcome, error: null, Now, TimeSpan.FromMilliseconds(250));

        Assert.Equal(expectedCount, execution.AttemptCount);
        Assert.Equal(250, execution.Attempts.Single().DurationMilliseconds);
        Assert.Equal(execution.Id, execution.Attempts.Single().StepExecutionId);
    }

    [Fact]
    public void Trigger_WaitingRun_StoresTheChangeInTheContext_AndWakesTheRun()
    {
        var run = PipelineRun.Start("flow", 1, Guid.CreateVersion7(), PipelineContextSerializer.Serialize(new FlowContext()), Now);
        run.Wait(Now, deadline: null);

        var triggered = run.Trigger<FlowContext>(context => context.Name = "delivered", Now);

        Assert.True(triggered);
        Assert.Equal("delivered", PipelineContextSerializer.Deserialize<FlowContext>(run.ContextJson).Name);
        Assert.Equal(RunStatus.Running, run.Status);
        Assert.True(run.IsDue(Now));
    }

    [Fact]
    public void Trigger_RunThatIsNotWaiting_IsRefusedAndChangesNothing()
    {
        var run = PipelineRun.Start("flow", 1, Guid.CreateVersion7(), PipelineContextSerializer.Serialize(new FlowContext()), Now);

        var triggered = run.Trigger<FlowContext>(context => context.Name = "delivered", Now);

        Assert.False(triggered);
        Assert.Equal("flow", PipelineContextSerializer.Deserialize<FlowContext>(run.ContextJson).Name);
    }

    [Fact]
    public void Lock_IsHeldUntilItExpires_AndUnlockReleasesIt()
    {
        var run = NewRun();
        Assert.False(run.IsLocked(Now));

        run.Lock("inline", Now.AddSeconds(10));
        var heldBefore = run.IsLocked(Now.AddSeconds(9));
        var heldAtExpiry = run.IsLocked(Now.AddSeconds(10));
        run.Unlock();

        Assert.True(heldBefore);
        Assert.False(heldAtExpiry);
        Assert.False(run.IsLocked(Now));
        Assert.Null(run.LockedBy);
    }

    private static PipelineRun NewRun() => PipelineRun.Start("flow", 1, Guid.CreateVersion7(), "{}", Now);

    private static PipelineRun RunIn(RunStatus status)
    {
        var run = NewRun();
        switch (status)
        {
            case RunStatus.Compensating:
                run.StartCompensating(CompensationCause.StepFailed, "failed", Now);
                break;
            case RunStatus.Completed:
                run.Complete();
                break;
            case RunStatus.Compensated:
                run.FinishCompensating();
                break;
            case RunStatus.RequiresAttention:
                run.RequireAttention("stuck");
                break;
        }

        Assert.Equal(status, run.Status);
        return run;
    }
}
