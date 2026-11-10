using NSubstitute;
using OrderPlatform.BuildingBlocks.Results;
using OrderPlatform.Ordering.Fulfilment;
using OrderPlatform.Ordering.Fulfilment.Steps;
using OrderPlatform.Ordering.Tests.Support;
using OrderPlatform.Pricing.Contracts;
using OrderPlatform.ProcessEngine.Steps;

namespace OrderPlatform.Ordering.Tests.Fulfilment;

public sealed class Step02CalculatePriceTests
{
    private static readonly PriceSnapshot Price = OrderingTestData.KeyboardPrice;

    private readonly IPricingService _pricing = Substitute.For<IPricingService>();
    private readonly Step02CalculatePrice _step;
    private readonly OrderFulfilmentContext _context = OrderFulfilmentContext.From(OrderingTestData.NewOrder());

    public Step02CalculatePriceTests() => _step = new Step02CalculatePrice(_pricing);

    [Fact]
    public void ShouldRun_AlwaysRuns() => Assert.True(_step.ShouldRun(_context));

    [Fact]
    public async Task Execute_Priced_StoresThePriceInTheContext()
    {
        PricingReturns(Result<PriceQuote>.Success(new PriceQuote([], Price.Subtotal, Price.Tax, Price.ShippingCharge, Price.Total)));

        var result = await _step.ExecuteAsync(_context, CancellationToken.None);

        Assert.Equal(StepOutcome.Success, result.Outcome);
        Assert.Equal(Price, _context.Price);
    }

    [Fact]
    public async Task Execute_AsksForEveryLineWithItsQuantity()
    {
        PricingReturns(Result<PriceQuote>.Success(new PriceQuote([], Price.Subtotal, Price.Tax, Price.ShippingCharge, Price.Total)));

        await _step.ExecuteAsync(_context, CancellationToken.None);

        await _pricing.Received(1).CalculateAsync(
            Arg.Is<PriceRequest>(request => request.Lines.SequenceEqual(new[] { new PriceRequestLine(OrderingTestData.Keyboard.ProductId, 1) })),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Execute_CannotBePriced_IsFatalWithTheErrorCode()
    {
        PricingReturns(Result<PriceQuote>.Failure(PricingErrors.ProductsNotAvailable([OrderingTestData.Keyboard.ProductId])));

        var result = await _step.ExecuteAsync(_context, CancellationToken.None);

        Assert.Equal(StepOutcome.FatalError, result.Outcome);
        Assert.StartsWith("pricing.products_not_available", result.Error, StringComparison.Ordinal);
        Assert.Null(_context.Price);
    }

    [Fact]
    public async Task Compensate_NothingToUndo_SucceedsTwiceAndKeepsThePrice()
    {
        _context.Price = Price;

        var first = await _step.CompensateAsync(_context, CancellationToken.None);
        var second = await _step.CompensateAsync(_context, CancellationToken.None);

        Assert.Equal(StepOutcome.Success, first.Outcome);
        Assert.Equal(StepOutcome.Success, second.Outcome);
        Assert.Equal(Price, _context.Price);
    }

    private void PricingReturns(Result<PriceQuote> result) =>
        _pricing.CalculateAsync(Arg.Any<PriceRequest>(), Arg.Any<CancellationToken>()).Returns(result);
}
