namespace OrderPlatform.ProcessEngine.Steps;

public enum StepOutcome
{
    Success,
    RetryableError,
    FatalError,
    Waiting,
    Interrupted,
}

public sealed record StepResult(StepOutcome Outcome, string? Error = null)
{
    public static StepResult Success() => new(StepOutcome.Success);

    public static StepResult Retryable(string error) => new(StepOutcome.RetryableError, error);

    public static StepResult Fatal(string error) => new(StepOutcome.FatalError, error);

    public static StepResult Waiting() => new(StepOutcome.Waiting);

    public static StepResult Interrupted() => new(StepOutcome.Interrupted);
}
