namespace OrderPlatform.ProcessEngine.Execution;

public enum InlineRunOutcome
{
    /// <summary>The goal was reached: the requested step ran successfully or was skipped, or the cancellation undid everything.</summary>
    Reached,

    /// <summary>Not there yet: a retry is scheduled, a step is waiting, or the time budget ran out. The worker continues the run.</summary>
    InProgress,

    /// <summary>A step failed before the requested step (the run is undoing what it did or needs attention), or an undo failed for good.</summary>
    Failed,
}
