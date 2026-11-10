using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OrderPlatform.ProcessEngine.Storage;

namespace OrderPlatform.ProcessEngine.Execution;

/// <summary>
/// Moves due runs forward in the background: orders placed without waiting, retries, failed steps to undo and
/// runs that were woken up. Each run is locked while it is processed, in its own unit of work, and saved after every move.
/// </summary>
public sealed partial class PipelineWorker(IServiceScopeFactory scopes, TimeProvider timeProvider, ILogger<PipelineWorker> logger) : BackgroundService
{
    internal const string Owner = "worker";
    internal const string ActivityName = "PipelineRun";

    // ponytail: fixed values and one polling loop; make them options and add more workers when the volume asks for it.
    private const int BatchSize = 20;
    private const int MaxMovesPerRun = 50;
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan LockTime = TimeSpan.FromMinutes(1);

    /// <summary>Processes the runs that are due now. Returns how many runs it moved.</summary>
    public async Task<int> RunOnceAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<Guid> due;
        await using (var scope = scopes.CreateAsyncScope())
        {
            due = await scope.ServiceProvider.GetRequiredService<IPipelineRunStore>()
                .FindDueIdsAsync(timeProvider.GetUtcNow(), BatchSize, cancellationToken);
        }

        var moved = 0;
        foreach (var runId in due)
        {
            // One trace per processed run, so every log line written while it moves carries the same trace id.
            // ponytail: this is a new trace, not the one of the request that placed the order. To join them,
            // store the request's traceparent on the run and pass it as the parent here.
            using var activity = new Activity(ActivityName).SetTag("pipeline.run_id", runId).Start();
            try
            {
                moved += await ProcessAsync(runId, cancellationToken) ? 1 : 0;
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                // One broken run (or one taken by the API in the meantime) must not stop the others; it is picked up again later.
                LogRunFailed(logger, exception, runId);
            }
        }

        return moved;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(PollInterval, timeProvider);
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                try
                {
                    await RunOnceAsync(stoppingToken);
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    LogPollFailed(logger, exception);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Shutdown: a step that was running is recorded as Interrupted and runs again after the restart.
        }
    }

    private async Task<bool> ProcessAsync(Guid runId, CancellationToken cancellationToken)
    {
        await using var scope = scopes.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<IPipelineRunStore>();
        var run = await store.FindAsync(runId, cancellationToken);
        var now = timeProvider.GetUtcNow();
        if (run is null || !run.IsDue(now) || run.IsLocked(now))
        {
            return false;
        }

        var executor = scope.ServiceProvider.GetServices<IPipelineExecutor>().SingleOrDefault(candidate => candidate.PipelineName == run.PipelineName)
            ?? throw new InvalidOperationException($"No pipeline named {run.PipelineName} is registered.");

        run.Lock(Owner, now + LockTime);
        await store.SaveChangesAsync(cancellationToken);
        try
        {
            for (var moves = 0; moves < MaxMovesPerRun && await executor.AdvanceAsync(run, cancellationToken); moves++)
            {
                await store.SaveChangesAsync(cancellationToken);
            }
        }
        finally
        {
            run.Unlock();
            await store.SaveChangesAsync(CancellationToken.None);
        }

        return true;
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Run {RunId} could not be processed; it will be picked up again")]
    private static partial void LogRunFailed(ILogger logger, Exception exception, Guid runId);

    [LoggerMessage(Level = LogLevel.Error, Message = "Looking for due runs failed; trying again on the next poll")]
    private static partial void LogPollFailed(ILogger logger, Exception exception);
}
