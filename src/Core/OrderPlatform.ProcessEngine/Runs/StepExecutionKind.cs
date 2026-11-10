namespace OrderPlatform.ProcessEngine.Runs;

public enum StepExecutionKind
{
    Execute,
    Compensate,
}

public enum StepExecutionStatus
{
    Pending,
    Succeeded,
    Failed,
    Skipped,
    Waiting,
}
