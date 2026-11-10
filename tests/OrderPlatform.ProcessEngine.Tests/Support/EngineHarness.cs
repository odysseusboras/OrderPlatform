using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using OrderPlatform.ProcessEngine.Execution;
using OrderPlatform.ProcessEngine.Pipelines;
using OrderPlatform.ProcessEngine.Runs;
using OrderPlatform.ProcessEngine.Storage;

namespace OrderPlatform.ProcessEngine.Tests.Support;

/// <summary>Wires the real executor with scripted steps and a fake clock, without a database.</summary>
public sealed class EngineHarness : IDisposable
{
    private readonly ServiceProvider _provider;
    private readonly IServiceScope _scope;

    public EngineHarness(params PipelineDefinition<FlowContext>[] definitions)
    {
        One = new StepOne(Journal);
        Two = new StepTwo(Journal);
        Three = new StepThree(Journal);
        Four = new StepFour(Journal);
        Five = new StepFive(Journal);

        var services = new ServiceCollection()
            .AddSingleton<TimeProvider>(Clock)
            .AddSingleton(typeof(ILogger<>), typeof(NullLogger<>))
            .AddSingleton(One)
            .AddSingleton(Two)
            .AddSingleton(Three)
            .AddSingleton(Four)
            .AddSingleton(Five)
            .AddSingleton<IPipelineObserver<FlowContext>>(Observer);

        foreach (var definition in definitions.DefaultIfEmpty(TestPipeline.Standard()))
        {
            services.AddPipeline(definition);
        }

        _provider = services.BuildServiceProvider();
        _scope = _provider.CreateScope();
        Executor = _scope.ServiceProvider.GetRequiredService<IPipelineExecutor>();
    }

    public FakeTimeProvider Clock { get; } = new(new DateTimeOffset(2026, 11, 10, 9, 0, 0, TimeSpan.Zero));

    public StepJournal Journal { get; } = new();

    public RecordingObserver Observer { get; } = new();

    public StepOne One { get; }

    public StepTwo Two { get; }

    public StepThree Three { get; }

    public StepFour Four { get; }

    public StepFive Five { get; }

    public IPipelineExecutor Executor { get; }

    /// <summary>The inline runner over this harness's executor, saving through the given store.</summary>
    public IPipelineRunner Runner(IPipelineRunStore store) => new PipelineRunner([Executor], store, Clock);

    public PipelineRun NewRun(int version = 1) =>
        PipelineRun.Start(TestPipeline.PipelineName, version, Guid.CreateVersion7(), PipelineContextSerializer.Serialize(new FlowContext()), Clock.GetUtcNow());

    public Task<bool> AdvanceAsync(PipelineRun run, CancellationToken cancellationToken = default) =>
        Executor.AdvanceAsync(run, cancellationToken);

    /// <summary>Advances while the run is due now. The clock does not move, so scheduled retries stop the loop.</summary>
    public async Task<PipelineRun> AdvanceWhileDueAsync(PipelineRun run, int limit = 50)
    {
        for (var calls = 0; calls < limit && await AdvanceAsync(run); calls++)
        {
        }

        return run;
    }

    public static FlowContext ContextOf(PipelineRun run) => PipelineContextSerializer.Deserialize<FlowContext>(run.ContextJson);

    public void Dispose()
    {
        _scope.Dispose();
        _provider.Dispose();
    }
}
