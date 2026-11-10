using OrderPlatform.BuildingBlocks.Auditing;
using OrderPlatform.ProcessEngine.Steps;

namespace OrderPlatform.ProcessEngine.Runs;

/// <summary>The execution or the compensation of one step within a run, with every attempt.</summary>
public sealed class StepExecution : AuditableEntity
{
    private readonly List<StepAttempt> _attempts = [];

    private StepExecution()
    {
    }

    internal StepExecution(Guid runId, string stepKey, int stepIndex, StepExecutionKind kind)
    {
        RunId = runId;
        StepKey = stepKey;
        StepIndex = stepIndex;
        Kind = kind;
        Status = StepExecutionStatus.Pending;
    }

    public Guid RunId { get; private set; }

    public string StepKey { get; private set; } = string.Empty;

    public int StepIndex { get; private set; }

    public StepExecutionKind Kind { get; private set; }

    public StepExecutionStatus Status { get; private set; }

    /// <summary>Attempts that count towards the retry limit. An interrupted attempt (shutdown) or a waiting check does not count.</summary>
    public int AttemptCount { get; private set; }

    public string? LastError { get; private set; }

    public DateTimeOffset? CompletedAt { get; private set; }

    public IReadOnlyList<StepAttempt> Attempts => _attempts;

    internal void RecordAttempt(StepOutcome outcome, string? error, DateTimeOffset startedAt, TimeSpan duration)
    {
        if (outcome is not (StepOutcome.Interrupted or StepOutcome.Waiting))
        {
            AttemptCount++;
        }

        LastError = error;
        _attempts.Add(new StepAttempt(Id, _attempts.Count + 1, outcome, error, startedAt, duration));
    }

    internal void Succeed(DateTimeOffset at) => Finish(StepExecutionStatus.Succeeded, at);

    internal void Fail(DateTimeOffset at) => Finish(StepExecutionStatus.Failed, at);

    internal void Skip(DateTimeOffset at) => Finish(StepExecutionStatus.Skipped, at);

    internal void MarkWaiting() => Status = StepExecutionStatus.Waiting;

    private void Finish(StepExecutionStatus status, DateTimeOffset at)
    {
        Status = status;
        CompletedAt = at;
    }
}
