namespace OrderPlatform.ProcessEngine.Pipelines;

public enum TriggerMode
{
    /// <summary>The worker runs the step as soon as the previous one succeeds.</summary>
    Auto,

    /// <summary>The run waits until the step is triggered through the API.</summary>
    OnDemand,
}
