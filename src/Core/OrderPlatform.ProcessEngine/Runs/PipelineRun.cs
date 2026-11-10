using OrderPlatform.BuildingBlocks.Auditing;
using OrderPlatform.ProcessEngine.Execution;

namespace OrderPlatform.ProcessEngine.Runs;

/// <summary>
/// One execution of one pipeline version for one business object (for example an order).
/// Holds where the run is, the serialized context and every step execution, so it can continue after a restart.
/// </summary>
public sealed class PipelineRun : AuditableEntity
{
    private readonly List<StepExecution> _steps = [];

    private PipelineRun()
    {
    }

    public string PipelineName { get; private set; } = string.Empty;

    /// <summary>Pinned when the run starts and never changed: the run follows this version until it ends.</summary>
    public int PipelineVersion { get; private set; }

    /// <summary>The business object the run works for, for example the order id. One run per object and pipeline.</summary>
    public Guid BusinessKey { get; private set; }

    public RunStatus Status { get; private set; }

    public RunDirection Direction { get; private set; }

    /// <summary>The step to run next (forward) or the highest step that may still need undoing (backward).</summary>
    public int NextStepIndex { get; private set; }

    /// <summary>True once the pivot step succeeded: from then on nothing is undone and cancellation is refused.</summary>
    public bool PivotPassed { get; private set; }

    public string ContextJson { get; private set; } = string.Empty;

    /// <summary>When the worker should pick the run up again. Null while nothing is scheduled.</summary>
    public DateTimeOffset? NextAttemptAt { get; private set; }

    public DateTimeOffset? WaitingSince { get; private set; }

    public DateTimeOffset? CancelRequestedAt { get; private set; }

    public CompensationCause? CompensationCause { get; private set; }

    /// <summary>Why the run is compensating or needs attention.</summary>
    public string? Reason { get; private set; }

    /// <summary>Who holds the run right now (the API running it inline, or a worker). Nobody else may advance it until the lock expires.</summary>
    public string? LockedBy { get; private set; }

    public DateTimeOffset? LockedUntil { get; private set; }

    public byte[] RowVersion { get; private set; } = [];

    public IReadOnlyList<StepExecution> Steps => _steps;

    public bool IsFinished => Status is RunStatus.Completed or RunStatus.Compensated;

    /// <summary>Cancellation is possible until the pivot succeeds and while the run is still moving forward.</summary>
    public bool CanCancel => !PivotPassed && Status is RunStatus.Running or RunStatus.Waiting;

    /// <summary>True while someone holds the run. An expired lock (its holder crashed) no longer counts.</summary>
    public bool IsLocked(DateTimeOffset now) => LockedUntil is { } until && until > now;

    public bool IsDue(DateTimeOffset now) =>
        Status is RunStatus.Running or RunStatus.Waiting or RunStatus.Compensating
        && NextAttemptAt is { } dueAt
        && dueAt <= now;

    /// <summary>Asks the run to undo what it did. Refused after the pivot or once the run has stopped moving forward.</summary>
    public bool RequestCancel(DateTimeOffset now)
    {
        if (!CanCancel)
        {
            return false;
        }

        CancelRequestedAt = now;
        NextAttemptAt = now;
        return true;
    }

    /// <summary>Wakes a run waiting at an OnDemand step, after its trigger stored what the step needs in the context.</summary>
    public bool Wake(DateTimeOffset now)
    {
        if (Status != RunStatus.Waiting)
        {
            return false;
        }

        Status = RunStatus.Running;
        NextAttemptAt = now;
        return true;
    }

    /// <summary>
    /// Triggers a run that is waiting at an OnDemand step: stores what the step waits for in the context and wakes the run.
    /// Refused when the run is not waiting.
    /// </summary>
    public bool Trigger<TContext>(Action<TContext> update, DateTimeOffset now)
        where TContext : class
    {
        ArgumentNullException.ThrowIfNull(update);
        if (Status != RunStatus.Waiting)
        {
            return false;
        }

        var context = PipelineContextSerializer.Deserialize<TContext>(ContextJson);
        update(context);
        ContextJson = PipelineContextSerializer.Serialize(context);
        return Wake(now);
    }

    internal static PipelineRun Start(string pipelineName, int pipelineVersion, Guid businessKey, string contextJson, DateTimeOffset now) =>
        new()
        {
            PipelineName = pipelineName,
            PipelineVersion = pipelineVersion,
            BusinessKey = businessKey,
            ContextJson = contextJson,
            Status = RunStatus.Running,
            Direction = RunDirection.Forward,
            NextAttemptAt = now,
        };

    internal void Lock(string owner, DateTimeOffset until)
    {
        LockedBy = owner;
        LockedUntil = until;
    }

    internal void Unlock()
    {
        LockedBy = null;
        LockedUntil = null;
    }

    /// <summary>True once the step with this key ran successfully or was skipped.</summary>
    internal bool HasPassed(string stepKey) =>
        _steps.Exists(step => step.Kind == StepExecutionKind.Execute
            && step.StepKey == stepKey
            && step.Status is StepExecutionStatus.Succeeded or StepExecutionStatus.Skipped);

    internal StepExecution ExecutionFor(string stepKey, int stepIndex, StepExecutionKind kind)
    {
        var execution = _steps.SingleOrDefault(step => step.StepIndex == stepIndex && step.Kind == kind);
        if (execution is null)
        {
            execution = new StepExecution(Id, stepKey, stepIndex, kind);
            _steps.Add(execution);
        }

        return execution;
    }

    internal StepExecution? FindExecution(int stepIndex, StepExecutionKind kind) =>
        _steps.SingleOrDefault(step => step.StepIndex == stepIndex && step.Kind == kind);

    internal void UpdateContext(string contextJson) => ContextJson = contextJson;

    internal void ScheduleAt(DateTimeOffset at) => NextAttemptAt = at;

    internal void MoveForward(DateTimeOffset now)
    {
        NextStepIndex++;
        Status = RunStatus.Running;
        WaitingSince = null;
        NextAttemptAt = now;
    }

    internal void MoveBackward(DateTimeOffset now)
    {
        NextStepIndex--;
        NextAttemptAt = now;
    }

    internal void PassPivot() => PivotPassed = true;

    internal void Complete()
    {
        Status = RunStatus.Completed;
        NextAttemptAt = null;
    }

    internal void Wait(DateTimeOffset now, DateTimeOffset? deadline)
    {
        Status = RunStatus.Waiting;
        WaitingSince ??= now;
        NextAttemptAt = deadline;
    }

    internal void StartCompensating(CompensationCause cause, string reason, DateTimeOffset now)
    {
        Direction = RunDirection.Backward;
        Status = RunStatus.Compensating;
        CompensationCause = cause;
        Reason = reason;
        NextStepIndex--;
        WaitingSince = null;
        NextAttemptAt = now;
    }

    internal void FinishCompensating()
    {
        Status = RunStatus.Compensated;
        NextAttemptAt = null;
    }

    internal void RequireAttention(string reason)
    {
        Status = RunStatus.RequiresAttention;
        Reason = reason;
        NextAttemptAt = null;
    }
}
