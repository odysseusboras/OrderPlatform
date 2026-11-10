using OrderPlatform.ProcessEngine.Runs;
using OrderPlatform.ProcessEngine.Steps;
using OrderPlatform.ProcessEngine.Tests.Support;

namespace OrderPlatform.ProcessEngine.Tests.Execution;

public sealed class PipelineExecutorObserverTests : IDisposable
{
    private readonly EngineHarness _engine = new();

    [Fact]
    public async Task Advance_CallsTheObserverAfterTheMove_WithTheUpdatedRunAndContext()
    {
        var run = _engine.NewRun();

        await _engine.AdvanceAsync(run);

        var call = Assert.Single(_engine.Observer.Calls);
        Assert.Equal(1, call.NextStepIndex);
        Assert.Equal(["one"], call.Visited);
    }

    [Fact]
    public async Task Advance_ObserverSeesCompensationStart()
    {
        _engine.Two.ThenExecute(StepResult.Fatal("out of stock"));
        var run = _engine.NewRun();

        await _engine.AdvanceAsync(run);
        await _engine.AdvanceAsync(run);

        Assert.Equal(RunStatus.Compensating, _engine.Observer.Calls[^1].Status);
    }

    [Fact]
    public async Task Advance_ContextChangedByTheObserver_IsStored()
    {
        _engine.Observer.RenameTo = "renamed";
        var run = _engine.NewRun();

        await _engine.AdvanceAsync(run);

        Assert.Equal("renamed", EngineHarness.ContextOf(run).Name);
    }

    [Fact]
    public async Task Advance_NotDue_DoesNotCallTheObserver()
    {
        var run = await _engine.AdvanceWhileDueAsync(_engine.NewRun());
        var calls = _engine.Observer.Calls.Count;

        await _engine.AdvanceAsync(run);

        Assert.Equal(calls, _engine.Observer.Calls.Count);
    }

    public void Dispose() => _engine.Dispose();
}
