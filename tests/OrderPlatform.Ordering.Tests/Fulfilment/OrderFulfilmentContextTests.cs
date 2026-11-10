using OrderPlatform.Integrations.Shipping.Contracts;
using OrderPlatform.Ordering.Fulfilment;
using OrderPlatform.Ordering.Tests.Support;
using OrderPlatform.ProcessEngine.Execution;

namespace OrderPlatform.Ordering.Tests.Fulfilment;

/// <summary>The context is stored as JSON after every step, so every value must survive the round trip.</summary>
public sealed class OrderFulfilmentContextTests
{
    [Fact]
    public void SerializeThenDeserialize_KeepsEveryValue()
    {
        var context = OrderFulfilmentContext.From(OrderingTestData.NewOrder());
        context.Price = OrderingTestData.KeyboardPrice;
        context.ReservationId = "res-1";
        context.DeliveredAt = new DateTimeOffset(2026, 11, 12, 10, 0, 0, TimeSpan.Zero);

        var copy = PipelineContextSerializer.Deserialize<OrderFulfilmentContext>(PipelineContextSerializer.Serialize(context));

        Assert.Equal(context.OrderId, copy.OrderId);
        Assert.Equal(context.Lines, copy.Lines);
        Assert.Equal(context.ShippingAddress, copy.ShippingAddress);
        Assert.Equal(context.Price, copy.Price);
        Assert.Equal("res-1", copy.ReservationId);
        Assert.Null(copy.ShipmentId);
        Assert.Equal(context.DeliveredAt, copy.DeliveredAt);
    }

    [Fact]
    public void Serialize_WritesTheCurrencyByName()
    {
        var context = OrderFulfilmentContext.From(OrderingTestData.NewOrder());
        context.Price = OrderingTestData.KeyboardPrice;

        Assert.Contains("\"EUR\"", PipelineContextSerializer.Serialize(context), StringComparison.Ordinal);
    }

    [Fact]
    public void From_CopiesTheOrderInputs_AndLeavesTheResultsEmpty()
    {
        var order = OrderingTestData.NewOrder();

        var context = OrderFulfilmentContext.From(order);

        Assert.Equal(order.Id, context.OrderId);
        Assert.Equal([new FulfilmentLine(OrderingTestData.Keyboard.ProductId, "KEYBOARD-MX", 1, true)], context.Lines);
        Assert.Equal("tok_visa", context.PaymentToken);
        Assert.Equal(new ShippingAddress("Ada Lovelace", "1 Main St", "Athens", "10557", "GR"), context.ShippingAddress);
        Assert.Null(context.Price);
    }
}
