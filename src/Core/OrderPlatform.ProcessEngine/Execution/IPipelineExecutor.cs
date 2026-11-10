using OrderPlatform.ProcessEngine.Runs;

namespace OrderPlatform.ProcessEngine.Execution;

/// <summary>Moves runs of one pipeline forward. The caller loads the run before and saves it after every call.</summary>
public interface IPipelineExecutor
{
    string PipelineName { get; }

    /// <summary>
    /// Makes one move and updates the run in memory: runs one step (or one undo), skips one step, or completes the run.
    /// Returns false when the run was not due.
    /// </summary>
    Task<bool> AdvanceAsync(PipelineRun run, CancellationToken cancellationToken);
}
