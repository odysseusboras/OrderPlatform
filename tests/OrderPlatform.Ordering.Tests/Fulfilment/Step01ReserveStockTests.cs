using NSubstitute;
using OrderPlatform.BuildingBlocks.Integration;
using OrderPlatform.BuildingBlocks.Money;
using OrderPlatform.Integrations.Inventory.Contracts;
using OrderPlatform.Ordering.Fulfilment;
using OrderPlatform.Ordering.Fulfilment.Steps;
using OrderPlatform.Ordering.Orders;
using OrderPlatform.Ordering.Tests.Support;
using OrderPlatform.ProcessEngine.Steps;
using static OrderPlatform.Ordering.Tests.Support.OrderingTestData;

namespace OrderPlatform.Ordering.Tests.Fulfilment;

public sealed class Step01ReserveStockTests
{
    private static readonly DateTimeOffset Expiry = new(2026, 11, 10, 9, 30, 0, TimeSpan.Zero);

    private readonly IInventoryGateway _inventory = Substitute.For<IInventoryGateway>();
    private readonly Step01ReserveStock _step;
    private readonly OrderFulfilmentContext _context = ContextFor(
        new NewOrderLine(Keyboard.ProductId, Keyboard.Sku, 2, true),
        new NewOrderLine(Ebook.ProductId, Ebook.Sku, 1, false));

    public Step01ReserveStockTests() => _step = new Step01ReserveStock(_inventory);

    [Fact]
    public void ShouldRun_OrderWithAPhysicalItem_Runs() => Assert.True(_step.ShouldRun(_context));

    [Fact]
    public void ShouldRun_DigitalOnlyOrder_IsSkipped() =>
        Assert.False(_step.ShouldRun(ContextFor(new NewOrderLine(Ebook.ProductId, Ebook.Sku, 1, false))));

    [Fact]
    public async Task Execute_Reserved_StoresTheReservationId()
    {
        ReserveReturns(GatewayResult<StockReservation>.Success(new StockReservation("res-1", Expiry)));

        var result = await _step.ExecuteAsync(_context, CancellationToken.None);

        Assert.Equal(StepOutcome.Success, result.Outcome);
        Assert.Equal("res-1", _context.ReservationId);
    }

    [Fact]
    public async Task Execute_ReservesOnlyThePhysicalItems_WithAKeyThatIsTheSameOnEveryRetry()
    {
        ReserveReturns(GatewayResult<StockReservation>.Success(new StockReservation("res-1", Expiry)));

        await _step.ExecuteAsync(_context, CancellationToken.None);
        await _step.ExecuteAsync(_context, CancellationToken.None);

        await _inventory.Received(2).ReserveAsync(
            Arg.Is<ReserveStockRequest>(request =>
                request.IdempotencyKey == $"{_context.OrderId}:ReserveStock:Execute"
                && request.OrderId == _context.OrderId
                && request.Lines.SequenceEqual(new[] { new StockLine("KEYBOARD-MX", 2) })),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Execute_OutOfStock_IsFatalAndReservesNothing()
    {
        ReserveReturns(GatewayResult<StockReservation>.Permanent("Out of stock: KEYBOARD-MX."));

        var result = await _step.ExecuteAsync(_context, CancellationToken.None);

        Assert.Equal(StepOutcome.FatalError, result.Outcome);
        Assert.Equal("Out of stock: KEYBOARD-MX.", result.Error);
        Assert.Null(_context.ReservationId);
    }

    [Fact]
    public async Task Execute_InventoryUnavailable_IsRetryable()
    {
        ReserveReturns(GatewayResult<StockReservation>.Transient("timeout"));

        var result = await _step.ExecuteAsync(_context, CancellationToken.None);

        Assert.Equal(StepOutcome.RetryableError, result.Outcome);
        Assert.Null(_context.ReservationId);
    }

    [Fact]
    public async Task Compensate_Reserved_ReleasesTheReservationAndForgetsIt()
    {
        _context.ReservationId = "res-1";
        _inventory.ReleaseAsync(Arg.Any<ReleaseStockRequest>(), Arg.Any<CancellationToken>()).Returns(GatewayResult.Success());

        var result = await _step.CompensateAsync(_context, CancellationToken.None);

        Assert.Equal(StepOutcome.Success, result.Outcome);
        Assert.Null(_context.ReservationId);
        await _inventory.Received(1).ReleaseAsync(
            new ReleaseStockRequest($"{_context.OrderId}:ReserveStock:Compensate", "res-1"), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Compensate_CalledTwice_ReleasesOnlyOnce()
    {
        _context.ReservationId = "res-1";
        _inventory.ReleaseAsync(Arg.Any<ReleaseStockRequest>(), Arg.Any<CancellationToken>()).Returns(GatewayResult.Success());

        await _step.CompensateAsync(_context, CancellationToken.None);
        var second = await _step.CompensateAsync(_context, CancellationToken.None);

        Assert.Equal(StepOutcome.Success, second.Outcome);
        await _inventory.Received(1).ReleaseAsync(Arg.Any<ReleaseStockRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Compensate_NothingReserved_SucceedsWithoutCallingTheInventory()
    {
        var result = await _step.CompensateAsync(_context, CancellationToken.None);

        Assert.Equal(StepOutcome.Success, result.Outcome);
        await _inventory.DidNotReceive().ReleaseAsync(Arg.Any<ReleaseStockRequest>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(GatewayOutcome.TransientFailure, StepOutcome.RetryableError)]
    [InlineData(GatewayOutcome.PermanentFailure, StepOutcome.FatalError)]
    public async Task Compensate_ReleaseFails_KeepsTheReservationIdForTheNextTry(GatewayOutcome outcome, StepOutcome expected)
    {
        _context.ReservationId = "res-1";
        _inventory.ReleaseAsync(Arg.Any<ReleaseStockRequest>(), Arg.Any<CancellationToken>()).Returns(new GatewayResult(outcome, "failed"));

        var result = await _step.CompensateAsync(_context, CancellationToken.None);

        Assert.Equal(expected, result.Outcome);
        Assert.Equal("res-1", _context.ReservationId);
    }

    private static OrderFulfilmentContext ContextFor(params NewOrderLine[] lines) =>
        OrderFulfilmentContext.From(Order.Create(Alice, Address, "tok_visa", Currency.EUR, lines));

    private void ReserveReturns(GatewayResult<StockReservation> result) =>
        _inventory.ReserveAsync(Arg.Any<ReserveStockRequest>(), Arg.Any<CancellationToken>()).Returns(result);
}
