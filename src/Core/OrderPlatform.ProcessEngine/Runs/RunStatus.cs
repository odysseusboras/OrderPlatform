namespace OrderPlatform.ProcessEngine.Runs;

public enum RunStatus
{
    /// <summary>Moving forward through the steps.</summary>
    Running,

    /// <summary>Stopped at an OnDemand step until it is triggered, or until its timeout.</summary>
    Waiting,

    /// <summary>Undoing the succeeded steps in reverse order, after a fatal error or a cancel request.</summary>
    Compensating,

    /// <summary>Every step ran. Final.</summary>
    Completed,

    /// <summary>Every succeeded step was undone. Final.</summary>
    Compensated,

    /// <summary>Stopped: retries ran out, a step failed after the pivot, or an undo failed. A person must decide.</summary>
    RequiresAttention,
}

public enum RunDirection
{
    Forward,
    Backward,
}

public enum CompensationCause
{
    StepFailed,
    Cancelled,
}
