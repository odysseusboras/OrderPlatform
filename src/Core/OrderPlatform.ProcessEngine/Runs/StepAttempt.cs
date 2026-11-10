using OrderPlatform.BuildingBlocks.Auditing;
using OrderPlatform.ProcessEngine.Steps;

namespace OrderPlatform.ProcessEngine.Runs;

/// <summary>One try of one step: the audit trail of retries, kept for debugging and metrics.</summary>
public sealed class StepAttempt : AuditableEntity
{
    private StepAttempt()
    {
    }

    internal StepAttempt(Guid stepExecutionId, int number, StepOutcome outcome, string? error, DateTimeOffset startedAt, TimeSpan duration)
    {
        StepExecutionId = stepExecutionId;
        Number = number;
        Outcome = outcome;
        Error = error;
        StartedAt = startedAt;
        DurationMilliseconds = (long)duration.TotalMilliseconds;
    }

    public Guid StepExecutionId { get; private set; }

    public int Number { get; private set; }

    public StepOutcome Outcome { get; private set; }

    public string? Error { get; private set; }

    public DateTimeOffset StartedAt { get; private set; }

    public long DurationMilliseconds { get; private set; }
}
