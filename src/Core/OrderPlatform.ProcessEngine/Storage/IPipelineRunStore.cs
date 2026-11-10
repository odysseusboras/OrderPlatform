using OrderPlatform.ProcessEngine.Pipelines;
using OrderPlatform.ProcessEngine.Runs;

namespace OrderPlatform.ProcessEngine.Storage;

/// <summary>Creates, loads and saves pipeline runs through the shared unit of work.</summary>
public interface IPipelineRunStore
{
    /// <summary>
    /// Creates a run pinned to the definition's version and adds it to the unit of work without saving,
    /// so the caller saves it in the same transaction as its own changes (for example the new order).
    /// </summary>
    PipelineRun Start<TContext>(PipelineDefinition<TContext> definition, Guid businessKey, TContext context)
        where TContext : class;

    /// <summary>Loads a run with every step execution and attempt.</summary>
    Task<PipelineRun?> FindAsync(Guid runId, CancellationToken cancellationToken);

    Task<PipelineRun?> FindByBusinessKeyAsync(string pipelineName, Guid businessKey, CancellationToken cancellationToken);

    /// <summary>The runs a worker should pick up now: due, not finished and not held by anyone. Oldest first.</summary>
    Task<IReadOnlyList<Guid>> FindDueIdsAsync(DateTimeOffset now, int take, CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
