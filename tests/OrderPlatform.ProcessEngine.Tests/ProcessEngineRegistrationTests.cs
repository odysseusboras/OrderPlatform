using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using OrderPlatform.ProcessEngine.Execution;
using OrderPlatform.ProcessEngine.Pipelines;
using OrderPlatform.ProcessEngine.Tests.Support;

namespace OrderPlatform.ProcessEngine.Tests;

public sealed class ProcessEngineRegistrationTests
{
    [Fact]
    public void AddPipeline_InvalidDefinition_FailsWhenRegistered() =>
        Assert.Throws<InvalidOperationException>(() =>
            new ServiceCollection().AddPipeline(new TestPipeline(1, TestPipeline.Step<StepOne>(StepKind.Pivot), TestPipeline.Step<StepTwo>())));

    [Fact]
    public void AddPipeline_TwoVersions_RegistersOneExecutorThatKnowsBoth()
    {
        var services = new ServiceCollection()
            .AddSingleton(typeof(ILogger<>), typeof(NullLogger<>))
            .AddSingleton(new StepJournal())
            .AddPipeline(TestPipeline.Standard(1))
            .AddPipeline(TestPipeline.Standard(2));
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var executor = Assert.Single(scope.ServiceProvider.GetServices<IPipelineExecutor>());
        Assert.Equal(TestPipeline.PipelineName, executor.PipelineName);
        Assert.Equal(2, scope.ServiceProvider.GetServices<PipelineDefinition<FlowContext>>().Count());
    }

    [Fact]
    public void AddProcessEngine_RegistersTheInlineRunner() =>
        Assert.Contains(new ServiceCollection().AddProcessEngine(), service => service.ServiceType == typeof(IPipelineRunner));

    [Fact]
    public void AddPipelineWorker_RegistersTheWorkerAsAHostedService() =>
        Assert.Contains(new ServiceCollection().AddPipelineWorker(), service => service.ImplementationType == typeof(PipelineWorker));

    [Fact]
    public void AddPipeline_RegistersEveryStepType()
    {
        var services = new ServiceCollection().AddSingleton(new StepJournal()).AddPipeline(TestPipeline.Standard());
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        Assert.All(TestPipeline.Standard().Steps, step => Assert.NotNull(scope.ServiceProvider.GetService(step.StepType)));
    }
}
