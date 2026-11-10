namespace OrderPlatform.ProcessEngine.Tests.Support;

/// <summary>A small context for engine tests. Steps change it, so tests can check that it is stored with the run.</summary>
public sealed class FlowContext
{
    public string Name { get; set; } = "flow";

    public List<string> Visited { get; set; } = [];
}
