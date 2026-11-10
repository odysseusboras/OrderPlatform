using OrderPlatform.ProcessEngine.Runs;

namespace OrderPlatform.ProcessEngine.Execution;

/// <summary>Runs part of a run inside the caller's request, so the caller can answer with the result: the first steps, or a cancellation.</summary>
public interface IPipelineRunner
{
    /// <summary>
    /// Locks the run and saves it together with any pending changes of the caller (for example the new order),
    /// then advances and saves after every step until <paramref name="stepKey"/> has passed, the run cannot go on now,
    /// or <paramref name="budget"/> runs out. Only forward steps run here; a failed step is undone by the worker.
    /// The run is unlocked at the end; if the caller crashes, the lock expires and the worker continues the run.
    /// </summary>
    Task<InlineRunOutcome> RunUntilAsync(PipelineRun run, string stepKey, TimeSpan budget, CancellationToken cancellationToken);

    /// <summary>
    /// Runs a requested cancellation (<see cref="PipelineRun.RequestCancel"/>): locks the run and undoes every succeeded step
    /// in reverse order, saving after each one. Reached: everything is undone. InProgress: an undo has to be retried,
    /// the budget ran out, or someone else holds the run; the worker finishes it. Failed: an undo failed for good and the run needs attention.
    /// </summary>
    Task<InlineRunOutcome> RunCancellationAsync(PipelineRun run, TimeSpan budget, CancellationToken cancellationToken);
}
