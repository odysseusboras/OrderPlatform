namespace OrderPlatform.ProcessEngine.Pipelines;

/// <summary>
/// The position and settings of a step inside one pipeline version.
/// <see cref="Key"/> is stored with every run, so it must never change once a version is released.
/// </summary>
public sealed record PipelineStepDefinition(
    string Key,
    Type StepType,
    StepKind Kind,
    TriggerMode Trigger,
    RetryPolicy Retry,
    TimeSpan? Timeout = null);
