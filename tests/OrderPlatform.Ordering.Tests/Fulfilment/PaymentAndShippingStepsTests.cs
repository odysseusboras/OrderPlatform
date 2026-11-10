using NSubstitute;
using OrderPlatform.BuildingBlocks.Integration;
using OrderPlatform.BuildingBlocks.Money;
using OrderPlatform.Integrations.Inventory.Contracts;
using OrderPlatform.Integrations.Payments.Contracts;
using OrderPlatform.Integrations.Shipping.Contracts;
using OrderPlatform.Ordering.Fulfilment;
using OrderPlatform.Ordering.Fulfilment.Steps;
using OrderPlatform.Ordering.Orders;
using OrderPlatform.Ordering.Tests.Support;
using OrderPlatform.ProcessEngine.Steps;
using static OrderPlatform.Ordering.Tests.Support.OrderingTestData;

namespace OrderPlatform.Ordering.Tests.Fulfilment;

/// <summary>Steps 3 to 7: authorize, prepare shipment, capture, dispatch, await delivery (pivot).</summary>
public sealed class PaymentAndShippingStepsTests
{
    private readonly IPaymentGateway _payments = Substitute.For<IPaymentGateway>();
    private readonly IShippingGateway _shipping = Substitute.For<IShippingGateway>();
    private readonly IInventoryGateway _inventory = Substitute.For<IInventoryGateway>();
    private readonly OrderFulfilmentContext _context = PricedContext(KeyboardPrice, physical: true);

    [Fact]
    public async Task Authorize_Succeeds_StoresTheAuthorization_AndSendsOnlyTheTokenAndTheTotal()
    {
        _payments.AuthorizeAsync(Arg.Any<AuthorizePaymentRequest>(), Arg.Any<CancellationToken>())
            .Returns(GatewayResult<PaymentAuthorization>.Success(new PaymentAuthorization("auth-1")));

        var result = await new Step03AuthorizePayment(_payments).ExecuteAsync(_context, CancellationToken.None);

        Assert.Equal(StepOutcome.Success, result.Outcome);
        Assert.Equal("auth-1", _context.AuthorizationId);
        await _payments.Received(1).AuthorizeAsync(
            new AuthorizePaymentRequest($"{_context.OrderId}:AuthorizePayment:Execute", _context.OrderId, "tok_visa", Eur(104.08m)),
            Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(GatewayOutcome.PermanentFailure, StepOutcome.FatalError)]
    [InlineData(GatewayOutcome.TransientFailure, StepOutcome.RetryableError)]
    public async Task Authorize_Fails_MapsTheFailureAndStoresNothing(GatewayOutcome outcome, StepOutcome expected)
    {
        _payments.AuthorizeAsync(Arg.Any<AuthorizePaymentRequest>(), Arg.Any<CancellationToken>())
            .Returns(new GatewayResult<PaymentAuthorization>(outcome, Error: "declined"));

        var result = await new Step03AuthorizePayment(_payments).ExecuteAsync(_context, CancellationToken.None);

        Assert.Equal(expected, result.Outcome);
        Assert.Null(_context.AuthorizationId);
    }

    [Fact]
    public async Task Authorize_Compensate_VoidsOnce_AndIsSafeToCallAgain()
    {
        _context.AuthorizationId = "auth-1";
        _payments.VoidAsync(Arg.Any<VoidPaymentRequest>(), Arg.Any<CancellationToken>()).Returns(GatewayResult.Success());
        var step = new Step03AuthorizePayment(_payments);

        await step.CompensateAsync(_context, CancellationToken.None);
        var second = await step.CompensateAsync(_context, CancellationToken.None);

        Assert.Equal(StepOutcome.Success, second.Outcome);
        Assert.Null(_context.AuthorizationId);
        await _payments.Received(1).VoidAsync(new VoidPaymentRequest($"{_context.OrderId}:AuthorizePayment:Compensate", "auth-1"), Arg.Any<CancellationToken>());
    }

    [Fact]
    public void PaymentSteps_FreeOrder_AreSkipped()
    {
        var free = PricedContext(new PriceSnapshot(Eur(0), Eur(0), Eur(0), Eur(0)), physical: true);

        Assert.False(new Step03AuthorizePayment(_payments).ShouldRun(free));
        Assert.False(new Step05CapturePayment(_payments).ShouldRun(free));
        Assert.True(new Step03AuthorizePayment(_payments).ShouldRun(_context));
        Assert.True(new Step05CapturePayment(_payments).ShouldRun(_context));
    }

    [Fact]
    public void PaymentSteps_NoPriceYet_Throw_InsteadOfTreatingTheOrderAsFree()
    {
        var unpriced = OrderFulfilmentContext.From(NewOrder());

        Assert.Throws<InvalidOperationException>(() => new Step03AuthorizePayment(_payments).ShouldRun(unpriced));
    }

    [Fact]
    public async Task PrepareShipment_Succeeds_StoresShipmentAndTracking_ForThePhysicalItemsOnly()
    {
        _shipping.BookAsync(Arg.Any<BookShipmentRequest>(), Arg.Any<CancellationToken>())
            .Returns(GatewayResult<ShipmentBooking>.Success(new ShipmentBooking("shp-1", "TRK-1")));

        var result = await new Step04PrepareShipment(_shipping).ExecuteAsync(_context, CancellationToken.None);

        Assert.Equal(StepOutcome.Success, result.Outcome);
        Assert.Equal(("shp-1", "TRK-1"), (_context.ShipmentId, _context.TrackingNumber));
        await _shipping.Received(1).BookAsync(
            Arg.Is<BookShipmentRequest>(request =>
                request.IdempotencyKey == $"{_context.OrderId}:PrepareShipment:Execute"
                && request.Address == _context.ShippingAddress
                && request.Lines.SequenceEqual(new[] { new ShipmentLine("KEYBOARD-MX", 1) })),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PrepareShipment_Compensate_CancelsTheBookingOnce()
    {
        _context.ShipmentId = "shp-1";
        _context.TrackingNumber = "TRK-1";
        _shipping.CancelAsync(Arg.Any<CancelShipmentRequest>(), Arg.Any<CancellationToken>()).Returns(GatewayResult.Success());
        var step = new Step04PrepareShipment(_shipping);

        await step.CompensateAsync(_context, CancellationToken.None);
        await step.CompensateAsync(_context, CancellationToken.None);

        Assert.Null(_context.ShipmentId);
        Assert.Null(_context.TrackingNumber);
        await _shipping.Received(1).CancelAsync(Arg.Any<CancelShipmentRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public void ShippingSteps_DigitalOnlyOrder_AreSkipped()
    {
        var digital = PricedContext(KeyboardPrice, physical: false);

        Assert.False(new Step04PrepareShipment(_shipping).ShouldRun(digital));
        Assert.False(new Step06DispatchShipment(_shipping).ShouldRun(digital));
        Assert.False(new Step07AwaitDelivery(_inventory).ShouldRun(digital));
        Assert.True(new Step07AwaitDelivery(_inventory).ShouldRun(_context));
    }

    [Fact]
    public async Task Capture_Succeeds_StoresTheCapture_ForTheAuthorizedTotal()
    {
        _context.AuthorizationId = "auth-1";
        _payments.CaptureAsync(Arg.Any<CapturePaymentRequest>(), Arg.Any<CancellationToken>())
            .Returns(GatewayResult<PaymentCapture>.Success(new PaymentCapture("cap-1")));

        var result = await new Step05CapturePayment(_payments).ExecuteAsync(_context, CancellationToken.None);

        Assert.Equal(StepOutcome.Success, result.Outcome);
        Assert.Equal("cap-1", _context.CaptureId);
        await _payments.Received(1).CaptureAsync(
            new CapturePaymentRequest($"{_context.OrderId}:CapturePayment:Execute", "auth-1", Eur(104.08m)), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Capture_AuthorizationExpired_IsFatal_SoTheEarlierStepsAreUndone()
    {
        _context.AuthorizationId = "auth-1";
        _payments.CaptureAsync(Arg.Any<CapturePaymentRequest>(), Arg.Any<CancellationToken>())
            .Returns(GatewayResult<PaymentCapture>.Permanent("authorization expired"));

        var result = await new Step05CapturePayment(_payments).ExecuteAsync(_context, CancellationToken.None);

        Assert.Equal(StepOutcome.FatalError, result.Outcome);
        Assert.Null(_context.CaptureId);
    }

    [Fact]
    public async Task Capture_WithoutAnAuthorization_Throws() =>
        await Assert.ThrowsAsync<InvalidOperationException>(() => new Step05CapturePayment(_payments).ExecuteAsync(_context, CancellationToken.None));

    [Fact]
    public async Task Capture_Compensate_RefundsTheCapturedTotalOnce_AndLeavesNoAuthorizationToVoid()
    {
        _context.AuthorizationId = "auth-1";
        _context.CaptureId = "cap-1";
        _payments.RefundAsync(Arg.Any<RefundPaymentRequest>(), Arg.Any<CancellationToken>()).Returns(GatewayResult.Success());
        var step = new Step05CapturePayment(_payments);

        var result = await step.CompensateAsync(_context, CancellationToken.None);
        await step.CompensateAsync(_context, CancellationToken.None);

        Assert.Equal(StepOutcome.Success, result.Outcome);
        Assert.Null(_context.CaptureId);
        Assert.Null(_context.AuthorizationId);
        await _payments.Received(1).RefundAsync(
            new RefundPaymentRequest($"{_context.OrderId}:CapturePayment:Compensate", "cap-1", Eur(104.08m)), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Capture_Compensate_RefundFails_KeepsTheCapture_SoItIsTriedAgain()
    {
        _context.CaptureId = "cap-1";
        _payments.RefundAsync(Arg.Any<RefundPaymentRequest>(), Arg.Any<CancellationToken>()).Returns(GatewayResult.Transient("provider down"));

        var result = await new Step05CapturePayment(_payments).CompensateAsync(_context, CancellationToken.None);

        Assert.Equal(StepOutcome.RetryableError, result.Outcome);
        Assert.Equal("cap-1", _context.CaptureId);
    }

    [Fact]
    public async Task ThePivot_IsNeverUndone() =>
        await Assert.ThrowsAsync<InvalidOperationException>(() => new Step07AwaitDelivery(_inventory).CompensateAsync(_context, CancellationToken.None));

    [Fact]
    public async Task Dispatch_Succeeds_HandsOverTheParcel_AndLeavesTheStockReserved()
    {
        ReadyToDispatch();
        _shipping.DispatchAsync(Arg.Any<DispatchShipmentRequest>(), Arg.Any<CancellationToken>()).Returns(GatewayResult.Success());

        var result = await new Step06DispatchShipment(_shipping).ExecuteAsync(_context, CancellationToken.None);

        Assert.Equal(StepOutcome.Success, result.Outcome);
        await _shipping.Received(1).DispatchAsync(new DispatchShipmentRequest($"{_context.OrderId}:DispatchShipment:Execute", "shp-1"), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Dispatch_CarrierUnavailable_IsRetryable()
    {
        ReadyToDispatch();
        _shipping.DispatchAsync(Arg.Any<DispatchShipmentRequest>(), Arg.Any<CancellationToken>()).Returns(GatewayResult.Transient("carrier down"));

        var result = await new Step06DispatchShipment(_shipping).ExecuteAsync(_context, CancellationToken.None);

        Assert.Equal(StepOutcome.RetryableError, result.Outcome);
    }

    [Fact]
    public async Task Dispatch_Compensate_RecallsTheParcel()
    {
        ReadyToDispatch();
        _shipping.RecallAsync(Arg.Any<RecallShipmentRequest>(), Arg.Any<CancellationToken>()).Returns(GatewayResult.Success());

        var result = await new Step06DispatchShipment(_shipping).CompensateAsync(_context, CancellationToken.None);

        Assert.Equal(StepOutcome.Success, result.Outcome);
        await _shipping.Received(1).RecallAsync(new RecallShipmentRequest($"{_context.OrderId}:DispatchShipment:Compensate", "shp-1"), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AwaitDelivery_WaitsUntilTheDeliveryIsConfirmed_ThenTurnsTheReservationIntoASale()
    {
        ReadyToDispatch();
        _inventory.ConsumeAsync(Arg.Any<ConsumeStockRequest>(), Arg.Any<CancellationToken>()).Returns(GatewayResult.Success());
        var step = new Step07AwaitDelivery(_inventory);

        var before = await step.ExecuteAsync(_context, CancellationToken.None);
        await _inventory.DidNotReceive().ConsumeAsync(Arg.Any<ConsumeStockRequest>(), Arg.Any<CancellationToken>());
        _context.DeliveredAt = new DateTimeOffset(2026, 11, 12, 10, 0, 0, TimeSpan.Zero);
        var after = await step.ExecuteAsync(_context, CancellationToken.None);

        Assert.Equal(StepOutcome.Waiting, before.Outcome);
        Assert.Equal(StepOutcome.Success, after.Outcome);
        await _inventory.Received(1).ConsumeAsync(new ConsumeStockRequest($"{_context.OrderId}:ConsumeStock:Execute", "res-1"), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AwaitDelivery_StockCannotBeConsumed_ReturnsThatFailure()
    {
        ReadyToDispatch();
        _context.DeliveredAt = new DateTimeOffset(2026, 11, 12, 10, 0, 0, TimeSpan.Zero);
        _inventory.ConsumeAsync(Arg.Any<ConsumeStockRequest>(), Arg.Any<CancellationToken>()).Returns(GatewayResult.Transient("inventory down"));

        var result = await new Step07AwaitDelivery(_inventory).ExecuteAsync(_context, CancellationToken.None);

        Assert.Equal(StepOutcome.RetryableError, result.Outcome);
    }

    private static OrderFulfilmentContext PricedContext(PriceSnapshot price, bool physical)
    {
        var product = physical ? Keyboard : Ebook;
        var context = OrderFulfilmentContext.From(
            Order.Create(Alice, Address, "tok_visa", Currency.EUR, [new NewOrderLine(product.ProductId, product.Sku, 1, physical)]));
        context.Price = price;
        return context;
    }

    private void ReadyToDispatch()
    {
        _context.ShipmentId = "shp-1";
        _context.ReservationId = "res-1";
    }
}
