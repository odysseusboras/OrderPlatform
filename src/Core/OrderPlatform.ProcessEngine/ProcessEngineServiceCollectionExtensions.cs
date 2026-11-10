using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OrderPlatform.ProcessEngine.Execution;
using OrderPlatform.ProcessEngine.Pipelines;
using OrderPlatform.ProcessEngine.Storage;

namespace OrderPlatform.ProcessEngine;

public static class ProcessEngineServiceCollectionExtensions
{
    /// <summary>Registers the run store and the inline runner. Needs the shared <c>DbContext</c> registered by the persistence layer.</summary>
    public static IServiceCollection AddProcessEngine(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddScoped<IPipelineRunStore, PipelineRunStore>();
        services.TryAddScoped<IPipelineRunner, PipelineRunner>();
        return services;
    }

    /// <summary>Runs the background worker that moves due runs forward, in this host.</summary>
    public static IServiceCollection AddPipelineWorker(this IServiceCollection services) =>
        services.AddProcessEngine().AddHostedService<PipelineWorker>();

    /// <summary>
    /// Registers one version of a pipeline and the executor for its context type. The definition is validated here,
    /// so an invalid pipeline stops the application at startup. Register every version that still has unfinished runs.
    /// </summary>
    public static IServiceCollection AddPipeline<TContext>(this IServiceCollection services, PipelineDefinition<TContext> definition)
        where TContext : class
    {
        PipelineDefinitionValidator.Validate(definition);

        services.AddProcessEngine();
        services.AddSingleton(definition);
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IPipelineExecutor, PipelineExecutor<TContext>>());
        foreach (var step in definition.Steps)
        {
            services.TryAddScoped(step.StepType);
        }

        return services;
    }
}
