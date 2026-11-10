using OrderPlatform.ProcessEngine.Pipelines;
using OrderPlatform.ProcessEngine.Runs;
using OrderPlatform.ProcessEngine.Steps;
using OrderPlatform.ProcessEngine.Tests.Support;

namespace OrderPlatform.ProcessEngine.Tests.Execution;

public sealed class PipelineExecutorRetryTests : IDisposable
{
    private readonly EngineHarness _engine = new();

    [Fact]
    public async Task Advance_RetryableError_SchedulesTheSameStepAfterTheBaseDelay()
    {
        _engine.One.ThenExecute(StepResult.Retryable("busy"));
        var run = _engine.NewRun();
        var now = _engine.Clock.GetUtcNow();

        await _engine.AdvanceAsync(run);

        Assert.Equal(RunStatus.Running, run.Status);
        Assert.Equal(0, run.NextStepIndex);
        Assert.Equal(now.AddSeconds(1), run.NextAttemptAt);
    }

    [Fact]
    public async Task Advance_RetryScheduled_IsNotDueBeforeItsTime_AndRunsAfterIt()
    {
        _engine.One.ThenExecute(StepResult.Retryable("busy"));
        var run = _engine.NewRun();
        await _engine.AdvanceAsync(run);

        var tooEarly = await _engine.AdvanceAsync(run);
        _engine.Clock.Advance(TimeSpan.FromSeconds(1));
        var onTime = await _engine.AdvanceAsync(run);

        Assert.False(tooEarly);
        Assert.True(onTime);
        Assert.Equal(1, run.NextStepIndex);
    }

    [Fact]
    public async Task Advance_RepeatedRetryableErrors_DoubleTheDelayUpToTheMaximum()
    {
        var policy = new RetryPolicy(5, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(3));
        using var engine = new EngineHarness(new TestPipeline(1, TestPipeline.Step<StepOne>(retry: policy)));
        engine.One.ThenExecute(StepResult.Retryable("a"), StepResult.Retryable("b"), StepResult.Retryable("c"), StepResult.Retryable("d"));
        var run = engine.NewRun();
        var delays = new List<TimeSpan>();

        for (var attempt = 0; attempt < 4; attempt++)
        {
            var before = engine.Clock.GetUtcNow();
            await engine.AdvanceAsync(run);
            var delay = run.NextAttemptAt!.Value - before;
            delays.Add(delay);
            engine.Clock.Advance(delay);
        }

        Assert.Equal([1, 2, 3, 3], delays.Select(delay => delay.TotalSeconds));
    }

    [Fact]
    public async Task Advance_RetriesRunOut_RequiresAttentionWithoutUndoingAnything()
    {
        _engine.Two.ThenExecute(StepResult.Retryable("x"), StepResult.Retryable("y"), StepResult.Retryable("z"));
        var run = _engine.NewRun();

        for (var call = 0; call < 6; call++)
        {
            await _engine.AdvanceAsync(run);
            _engine.Clock.Advance(TimeSpan.FromMinutes(1));
        }

        var execution = run.FindExecution(1, StepExecutionKind.Execute)!;
        Assert.Equal(RunStatus.RequiresAttention, run.Status);
        Assert.Equal(StepExecutionStatus.Failed, execution.Status);
        Assert.Equal(3, execution.AttemptCount);
        Assert.Contains("StepTwo", run.Reason, StringComparison.Ordinal);
        Assert.Null(run.NextAttemptAt);
        Assert.Equal(0, _engine.One.CompensateCalls);
    }

    [Fact]
    public async Task Advance_StepThrows_IsTreatedAsARetryableError()
    {
        _engine.One.ThenExecute(_ => throw new HttpRequestException("connection refused"));
        var run = _engine.NewRun();

        await _engine.AdvanceAsync(run);

        var execution = run.FindExecution(0, StepExecutionKind.Execute)!;
        Assert.Equal(StepOutcome.RetryableError, execution.Attempts.Single().Outcome);
        Assert.Contains("HttpRequestException", execution.LastError, StringComparison.Ordinal);
        Assert.Equal(RunStatus.Running, run.Status);
        Assert.NotNull(run.NextAttemptAt);
    }

    [Fact]
    public async Task Advance_ShutdownCancelsTheStep_IsInterruptedAndDoesNotCountAsAnAttempt()
    {
        _engine.One.ThenExecute(token =>
        {
            token.ThrowIfCancellationRequested();
            return StepResult.Success();
        });
        var run = _engine.NewRun();
        using var shutdown = new CancellationTokenSource();
        await shutdown.CancelAsync();

        await _engine.AdvanceAsync(run, shutdown.Token);

        var execution = run.FindExecution(0, StepExecutionKind.Execute)!;
        Assert.Equal(StepOutcome.Interrupted, execution.Attempts.Single().Outcome);
        Assert.Equal(0, execution.AttemptCount);
        Assert.Equal(0, run.NextStepIndex);
        Assert.Equal(_engine.Clock.GetUtcNow(), run.NextAttemptAt);
    }

    [Fact]
    public async Task Advance_InterruptedResult_RunsTheSameStepAgainOnTheNextCall()
    {
        _engine.One.ThenExecute(StepResult.Interrupted());
        var run = _engine.NewRun();
        await _engine.AdvanceAsync(run);

        await _engine.AdvanceAsync(run);

        Assert.Equal(["execute one", "execute one"], _engine.Journal.Entries);
        Assert.Equal(1, run.NextStepIndex);
    }

    public void Dispose() => _engine.Dispose();
}
