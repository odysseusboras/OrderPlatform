namespace OrderPlatform.ProcessEngine.Pipelines;

/// <summary>
/// An ordered list of steps for one version of a pipeline. A released version is never edited:
/// changing the steps means adding a new version, and running processes stay on the version they started with.
/// </summary>
public abstract class PipelineDefinition<TContext>
    where TContext : class
{
    public abstract string Name { get; }

    public abstract int Version { get; }

    public abstract IReadOnlyList<PipelineStepDefinition> Steps { get; }
}
