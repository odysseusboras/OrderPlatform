namespace OrderPlatform.ProcessEngine.Pipelines;

public enum StepKind
{
    /// <summary>Can be undone when a later step fails or the process is cancelled.</summary>
    Compensatable,

    /// <summary>The go / no-go point: if it fails, earlier steps are undone; once it succeeds, the pipeline only moves forward.</summary>
    Pivot,

    /// <summary>Runs after the pivot: never undone, retried and then escalated.</summary>
    ForwardOnly,
}
