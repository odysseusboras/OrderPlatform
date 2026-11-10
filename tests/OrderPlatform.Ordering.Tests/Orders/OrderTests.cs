using OrderPlatform.BuildingBlocks.Money;
using OrderPlatform.Ordering.Orders;

namespace OrderPlatform.Ordering.Tests.Orders;

public sealed class OrderTests
{
    private static readonly ShippingAddress Address = new("Maria P.", "Solomou 24", "Athens", "18345", "GR");

    [Fact]
    public void Create_IsPending_WithItems_AndAFirstHistoryEntry()
    {
        var customer = Guid.NewGuid();
        var order = Order.Create(customer, Address, "tok_123", Currency.EUR, [Line(2), Line(1, isPhysical: false)]);

        Assert.Equal(customer, order.CustomerId);
        Assert.Equal(OrderStatus.Pending, order.Status);
        Assert.Equal(Currency.EUR, order.Currency);
        Assert.Equal(Address, order.ShippingAddress);
        Assert.Equal(2, order.Items.Count);
        Assert.All(order.Items, item => Assert.Equal(order.Id, item.OrderId));
        Assert.Null(order.Total);

        var first = Assert.Single(order.StatusHistory);
        Assert.Equal(1, first.Sequence);
        Assert.Null(first.FromStatus);
        Assert.Equal(OrderStatus.Pending, first.ToStatus);
    }

    [Fact]
    public void Create_WithoutLines_Throws()
    {
        Assert.Throws<ArgumentException>(() => Order.Create(Guid.NewGuid(), Address, "tok_123", Currency.EUR, []));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Create_WithQuantityNotAboveZero_Throws(int quantity)
    {
        Assert.Throws<ArgumentException>(() => Order.Create(Guid.NewGuid(), Address, "tok_123", Currency.EUR, [Line(quantity)]));
    }

    [Fact]
    public void Create_WithTheSameProductTwice_Throws()
    {
        var line = Line(1);

        Assert.Throws<ArgumentException>(() => Order.Create(Guid.NewGuid(), Address, "tok_123", Currency.EUR, [line, line with { Quantity = 2 }]));
    }

    [Fact]
    public void Create_WithoutCustomer_Throws()
    {
        Assert.Throws<ArgumentException>(() => Order.Create(Guid.Empty, Address, "tok_123", Currency.EUR, [Line(1)]));
    }

    [Fact]
    public void Create_WithAnUnknownCurrency_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Order.Create(Guid.NewGuid(), Address, "tok_123", (Currency)999, [Line(1)]));
    }

    [Fact]
    public void Create_WithoutPaymentToken_Throws()
    {
        Assert.ThrowsAny<ArgumentException>(() => Order.Create(Guid.NewGuid(), Address, " ", Currency.EUR, [Line(1)]));
    }

    [Fact]
    public void TransitionTo_AlongTheHappyPath_RecordsEveryChangeInOrder()
    {
        var order = NewOrder();

        order.TransitionTo(OrderStatus.Priced);
        order.TransitionTo(OrderStatus.Confirmed);
        order.TransitionTo(OrderStatus.Paid);
        order.TransitionTo(OrderStatus.Shipped);
        order.TransitionTo(OrderStatus.Delivered);

        Assert.Equal(OrderStatus.Delivered, order.Status);
        Assert.Equal([1, 2, 3, 4, 5, 6], order.StatusHistory.Select(change => change.Sequence));
        Assert.Equal(OrderStatus.Shipped, order.StatusHistory[^1].FromStatus);
        Assert.Equal(OrderStatus.Delivered, order.StatusHistory[^1].ToStatus);
    }

    [Fact]
    public void TransitionTo_StoresTheReason()
    {
        var order = NewOrder();

        order.TransitionTo(OrderStatus.Rejected, "Out of stock");

        Assert.Equal("Out of stock", order.StatusHistory[^1].Reason);
    }

    [Fact]
    public void TransitionTo_NotAllowed_Throws_AndKeepsTheStatus()
    {
        var order = NewOrder();

        Assert.Throws<InvalidOperationException>(() => order.TransitionTo(OrderStatus.Paid));
        Assert.Equal(OrderStatus.Pending, order.Status);
        Assert.Single(order.StatusHistory);
    }

    [Theory]
    [InlineData(OrderStatus.Delivered)]
    [InlineData(OrderStatus.Cancelled)]
    [InlineData(OrderStatus.Rejected)]
    [InlineData(OrderStatus.Failed)]
    public void FinalStatuses_AllowNoFurtherTransition(OrderStatus final)
    {
        var order = OrderIn(final);

        Assert.All(Enum.GetValues<OrderStatus>(), next => Assert.False(order.CanTransitionTo(next)));
    }

    [Theory]
    [InlineData(OrderStatus.Pending, true)]
    [InlineData(OrderStatus.Priced, true)]
    [InlineData(OrderStatus.Confirmed, true)]
    [InlineData(OrderStatus.Paid, true)]
    [InlineData(OrderStatus.Shipped, true)]
    [InlineData(OrderStatus.Delivered, false)]
    [InlineData(OrderStatus.Rejected, false)]
    public void CanCancel_UntilTheOrderIsDelivered(OrderStatus status, bool expected)
    {
        Assert.Equal(expected, OrderIn(status).CanCancel);
    }

    [Fact]
    public void ApplyPrice_StoresTheSnapshot_AndMovesToPriced()
    {
        var order = NewOrder();

        order.ApplyPrice(Eur(100m), Eur(24m), Eur(5m), Eur(129m));

        Assert.Equal(OrderStatus.Priced, order.Status);
        Assert.Equal(100m, order.SubtotalAmount);
        Assert.Equal(24m, order.TaxAmount);
        Assert.Equal(5m, order.ShippingChargeAmount);
        Assert.Equal(Eur(129m), order.Total);
    }

    [Fact]
    public void ApplyPrice_InAnotherCurrency_Throws()
    {
        Assert.Throws<ArgumentException>(() => NewOrder().ApplyPrice(Eur(100m), Eur(24m), Eur(5m), new Money(129m, Currency.USD)));
    }

    [Fact]
    public void ApplyPrice_WithANegativeAmount_Throws()
    {
        Assert.Throws<ArgumentException>(() => NewOrder().ApplyPrice(Eur(100m), Eur(-1m), Eur(5m), Eur(104m)));
    }

    [Fact]
    public void ApplyPrice_WhenNotPending_Throws()
    {
        var order = NewOrder();
        order.ApplyPrice(Eur(100m), Eur(24m), Eur(5m), Eur(129m));

        Assert.Throws<InvalidOperationException>(() => order.ApplyPrice(Eur(100m), Eur(24m), Eur(5m), Eur(129m)));
    }

    private static NewOrderLine Line(int quantity, bool isPhysical = true) =>
        new(Guid.NewGuid(), "SKU-" + Guid.NewGuid().ToString("N")[..6], quantity, isPhysical);

    private static Order NewOrder() => Order.Create(Guid.NewGuid(), Address, "tok_123", Currency.EUR, [Line(1)]);

    private static Money Eur(decimal amount) => new(amount, Currency.EUR);

    private static Order OrderIn(OrderStatus status)
    {
        var order = NewOrder();
        OrderStatus[] path = status switch
        {
            OrderStatus.Pending => [],
            OrderStatus.Priced => [OrderStatus.Priced],
            OrderStatus.Confirmed => [OrderStatus.Priced, OrderStatus.Confirmed],
            OrderStatus.Paid => [OrderStatus.Priced, OrderStatus.Confirmed, OrderStatus.Paid],
            OrderStatus.Shipped => [OrderStatus.Priced, OrderStatus.Confirmed, OrderStatus.Paid, OrderStatus.Shipped],
            OrderStatus.Delivered => [OrderStatus.Priced, OrderStatus.Confirmed, OrderStatus.Paid, OrderStatus.Shipped, OrderStatus.Delivered],
            OrderStatus.Cancelled => [OrderStatus.Cancelled],
            OrderStatus.Rejected => [OrderStatus.Rejected],
            OrderStatus.Failed => [OrderStatus.Priced, OrderStatus.Confirmed, OrderStatus.Failed],
            _ => throw new ArgumentOutOfRangeException(nameof(status)),
        };

        foreach (var next in path)
        {
            order.TransitionTo(next);
        }

        return order;
    }
}
