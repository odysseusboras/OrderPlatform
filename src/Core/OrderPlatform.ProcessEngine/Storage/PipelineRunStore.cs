using Microsoft.EntityFrameworkCore;
using OrderPlatform.ProcessEngine.Execution;
using OrderPlatform.ProcessEngine.Pipelines;
using OrderPlatform.ProcessEngine.Runs;

namespace OrderPlatform.ProcessEngine.Storage;

internal sealed class PipelineRunStore(DbContext database, TimeProvider timeProvider) : IPipelineRunStore
{
    public PipelineRun Start<TContext>(PipelineDefinition<TContext> definition, Guid businessKey, TContext context)
        where TContext : class
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(context);

        var run = PipelineRun.Start(
            definition.Name,
            definition.Version,
            businessKey,
            PipelineContextSerializer.Serialize(context),
            timeProvider.GetUtcNow());

        database.Set<PipelineRun>().Add(run);
        return run;
    }

    public Task<PipelineRun?> FindAsync(Guid runId, CancellationToken cancellationToken) =>
        WithSteps().SingleOrDefaultAsync(run => run.Id == runId, cancellationToken);

    public Task<PipelineRun?> FindByBusinessKeyAsync(string pipelineName, Guid businessKey, CancellationToken cancellationToken) =>
        WithSteps().SingleOrDefaultAsync(run => run.PipelineName == pipelineName && run.BusinessKey == businessKey, cancellationToken);

    public async Task<IReadOnlyList<Guid>> FindDueIdsAsync(DateTimeOffset now, int take, CancellationToken cancellationToken) =>
        await database.Set<PipelineRun>()
            .AsNoTracking()
            .Where(run => (run.Status == RunStatus.Running || run.Status == RunStatus.Waiting || run.Status == RunStatus.Compensating)
                && run.NextAttemptAt != null
                && run.NextAttemptAt <= now
                && (run.LockedUntil == null || run.LockedUntil <= now))
            .OrderBy(run => run.NextAttemptAt)
            .Take(take)
            .Select(run => run.Id)
            .ToListAsync(cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken) => database.SaveChangesAsync(cancellationToken);

    private IQueryable<PipelineRun> WithSteps() =>
        database.Set<PipelineRun>()
            .Include(run => run.Steps)
            .ThenInclude(step => step.Attempts)
            .AsSplitQuery();
}
