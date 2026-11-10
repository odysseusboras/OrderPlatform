using OrderPlatform.Ordering.Fulfilment;
using OrderPlatform.Ordering.Orders;
using OrderPlatform.Ordering.Tests.Support;
using OrderPlatform.ProcessEngine.Runs;

namespace OrderPlatform.Ordering.Tests.Fulfilment;

public sealed class OrderProgressTests
{
    private static readonly PriceSnapshot Price = OrderingTestData.KeyboardPrice;

    [Fact]
    public void Apply_PriceCalculated_MovesThePendingOrderToPricedWithTheAmounts()
    {
        var order = OrderingTestData.NewOrder();

        OrderProgress.Apply(order, RunStatus.Running, cause: null, reason: null, Price);

        Assert.Equal(OrderStatus.Priced, order.Status);
        Assert.Equal((79.90m, 19.18m, 5.00m, 104.08m), (order.SubtotalAmount, order.TaxAmount, order.ShippingChargeAmount, order.TotalAmount));
    }

    [Fact]
    public void Apply_CalledAgainAfterLaterSteps_DoesNotChangeThePricedOrder()
    {
        var order = OrderingTestData.NewOrder();
        OrderProgress.Apply(order, RunStatus.Running, null, null, Price);

        OrderProgress.Apply(order, RunStatus.Running, null, null, Price);

        Assert.Equal(OrderStatus.Priced, order.Status);
        Assert.Equal(2, order.StatusHistory.Count);
    }

    [Theory]
    [InlineData(OrderStatus.Confirmed, "AuthorizePayment")]
    [InlineData(OrderStatus.Paid, "AuthorizePayment", "CapturePayment")]
    [InlineData(OrderStatus.Shipped, "AuthorizePayment", "CapturePayment", "DispatchShipment")]
    public void Apply_StepsPassed_MoveTheOrderThroughItsLifecycle(OrderStatus expected, params string[] passed)
    {
        var order = OrderingTestData.NewOrder();

        OrderProgress.Apply(order, RunStatus.Running, null, null, Price, passed.ToHashSet());

        Assert.Equal(expected, order.Status);
    }

    [Fact]
    public void Apply_RunCompleted_MarksTheOrderDelivered_WithEveryStatusInItsHistory()
    {
        var order = OrderingTestData.NewOrder();

        OrderProgress.Apply(order, RunStatus.Completed, null, null, Price, new HashSet<string> { "AuthorizePayment", "CapturePayment", "DispatchShipment" });

        Assert.Equal(
            [OrderStatus.Pending, OrderStatus.Priced, OrderStatus.Confirmed, OrderStatus.Paid, OrderStatus.Shipped, OrderStatus.Delivered],
            order.StatusHistory.Select(change => change.ToStatus));
    }

    [Fact]
    public void Apply_LaterStepPassedButNotTheEarlierOne_DoesNotSkipAStatus()
    {
        var order = OrderingTestData.NewOrder();

        OrderProgress.Apply(order, RunStatus.Running, null, null, Price, new HashSet<string> { "CapturePayment" });

        Assert.Equal(OrderStatus.Priced, order.Status);
    }

    [Fact]
    public void Apply_NoPriceYet_LeavesTheOrderPending()
    {
        var order = OrderingTestData.NewOrder();

        OrderProgress.Apply(order, RunStatus.Running, null, null, price: null);

        Assert.Equal(OrderStatus.Pending, order.Status);
    }

    [Fact]
    public void Apply_PricingFailed_RejectsThePendingOrderWithTheReason()
    {
        var order = OrderingTestData.NewOrder();

        OrderProgress.Apply(order, RunStatus.Compensating, CompensationCause.StepFailed, "pricing.products_not_available", price: null);

        Assert.Equal(OrderStatus.Rejected, order.Status);
        Assert.Equal("pricing.products_not_available", order.StatusHistory[^1].Reason);
    }

    [Fact]
    public void Apply_LaterStepFailed_RejectsThePricedOrder()
    {
        var order = OrderingTestData.NewOrder();

        OrderProgress.Apply(order, RunStatus.Compensating, CompensationCause.StepFailed, "out of stock", Price);

        Assert.Equal([OrderStatus.Pending, OrderStatus.Priced, OrderStatus.Rejected], order.StatusHistory.Select(change => change.ToStatus));
    }

    [Fact]
    public void Apply_StepFailedAfterTheOrderWasConfirmed_MarksItFailed()
    {
        var order = OrderingTestData.NewOrder();
        OrderProgress.Apply(order, RunStatus.Running, null, null, Price);
        order.TransitionTo(OrderStatus.Confirmed);

        OrderProgress.Apply(order, RunStatus.Compensating, CompensationCause.StepFailed, "shipment booking failed", Price);

        Assert.Equal(OrderStatus.Failed, order.Status);
    }

    [Fact]
    public void Apply_CancelledByTheCustomer_CancelsTheOrder()
    {
        var order = OrderingTestData.NewOrder();
        OrderProgress.Apply(order, RunStatus.Running, null, null, Price);

        OrderProgress.Apply(order, RunStatus.Compensating, CompensationCause.Cancelled, "Cancellation requested.", Price);

        Assert.Equal(OrderStatus.Cancelled, order.Status);
    }

    [Fact]
    public void Apply_AlreadyRejected_StaysRejectedWhileTheRunFinishesUndoing()
    {
        var order = OrderingTestData.NewOrder();
        OrderProgress.Apply(order, RunStatus.Compensating, CompensationCause.StepFailed, "declined", null);

        OrderProgress.Apply(order, RunStatus.Compensated, CompensationCause.StepFailed, "declined", null);

        Assert.Equal(OrderStatus.Rejected, order.Status);
        Assert.Equal(2, order.StatusHistory.Count);
    }
}
