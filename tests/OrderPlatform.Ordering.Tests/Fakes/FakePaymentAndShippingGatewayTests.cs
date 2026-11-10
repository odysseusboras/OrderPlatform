using OrderPlatform.BuildingBlocks.Money;
using OrderPlatform.FakeExternalSystems.Payments;
using OrderPlatform.FakeExternalSystems.Shipping;
using OrderPlatform.Integrations.Payments.Contracts;
using OrderPlatform.Integrations.Shipping.Contracts;

namespace OrderPlatform.Ordering.Tests.Fakes;

public sealed class FakePaymentAndShippingGatewayTests
{
    private static readonly Money Amount = new(104.08m, Currency.EUR);

    [Fact]
    public async Task Payment_AuthorizeCaptureAndVoid_Succeed_AndTheSameKeyGivesTheSameAuthorization()
    {
        var payments = new FakePaymentGateway();
        var request = new AuthorizePaymentRequest("order-1:AuthorizePayment:Execute", Guid.CreateVersion7(), "tok_visa", Amount);

        var first = await payments.AuthorizeAsync(request, CancellationToken.None);
        var second = await payments.AuthorizeAsync(request, CancellationToken.None);
        var capture = await payments.CaptureAsync(new CapturePaymentRequest("order-1:CapturePayment:Execute", first.Value!.AuthorizationId, Amount), CancellationToken.None);
        var voided = await payments.VoidAsync(new VoidPaymentRequest("order-1:AuthorizePayment:Compensate", first.Value.AuthorizationId), CancellationToken.None);

        Assert.Equal(first.Value, second.Value);
        var refunded = await payments.RefundAsync(new RefundPaymentRequest("order-1:CapturePayment:Compensate", capture.Value!.CaptureId, Amount), CancellationToken.None);

        Assert.True(capture.IsSuccess);
        Assert.True(voided.IsSuccess);
        Assert.True(refunded.IsSuccess);
    }

    [Fact]
    public async Task Shipping_BookDispatchAndCancel_Succeed_AndTheSameKeyGivesTheSameBooking()
    {
        var shipping = new FakeShippingGateway();
        var request = new BookShipmentRequest(
            "order-1:PrepareShipment:Execute", Guid.CreateVersion7(), new ShippingAddress("Ada", "1 Main St", "Athens", "10557", "GR"), [new ShipmentLine("KEYBOARD-MX", 1)]);

        var first = await shipping.BookAsync(request, CancellationToken.None);
        var second = await shipping.BookAsync(request, CancellationToken.None);
        var dispatched = await shipping.DispatchAsync(new DispatchShipmentRequest("order-1:DispatchShipment:Execute", first.Value!.ShipmentId), CancellationToken.None);
        var cancelled = await shipping.CancelAsync(new CancelShipmentRequest("order-1:PrepareShipment:Compensate", first.Value.ShipmentId), CancellationToken.None);

        Assert.Equal(first.Value, second.Value);
        var recalled = await shipping.RecallAsync(new RecallShipmentRequest("order-1:DispatchShipment:Compensate", first.Value.ShipmentId), CancellationToken.None);

        Assert.True(dispatched.IsSuccess);
        Assert.True(cancelled.IsSuccess);
        Assert.True(recalled.IsSuccess);
    }

    [Fact]
    public async Task Calls_WithACancelledToken_Throw()
    {
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            new FakePaymentGateway().VoidAsync(new VoidPaymentRequest("key", "auth-1"), cancelled.Token));
        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            new FakeShippingGateway().DispatchAsync(new DispatchShipmentRequest("key", "shp-1"), cancelled.Token));
    }
}
