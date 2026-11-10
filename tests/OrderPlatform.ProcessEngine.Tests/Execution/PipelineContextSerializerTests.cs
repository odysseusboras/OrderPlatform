using OrderPlatform.ProcessEngine.Execution;
using OrderPlatform.ProcessEngine.Pipelines;
using OrderPlatform.ProcessEngine.Tests.Support;

namespace OrderPlatform.ProcessEngine.Tests.Execution;

public sealed class PipelineContextSerializerTests
{
    [Fact]
    public void SerializeThenDeserialize_KeepsTheValues()
    {
        var context = new FlowContext { Name = "order-42", Visited = ["one", "two"] };

        var copy = PipelineContextSerializer.Deserialize<FlowContext>(PipelineContextSerializer.Serialize(context));

        Assert.Equal("order-42", copy.Name);
        Assert.Equal(["one", "two"], copy.Visited);
    }

    [Fact]
    public void Serialize_WritesEnumsByName() =>
        Assert.Contains("\"ForwardOnly\"", PipelineContextSerializer.Serialize(new { Kind = StepKind.ForwardOnly }), StringComparison.Ordinal);

    [Fact]
    public void Deserialize_Null_Throws() =>
        Assert.Throws<InvalidOperationException>(() => PipelineContextSerializer.Deserialize<FlowContext>("null"));
}
