using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using OrderPlatform.BuildingBlocks.Money;
using OrderPlatform.Catalog.Contracts;
using OrderPlatform.Pricing.Contracts;

namespace OrderPlatform.Pricing.Tests;

public sealed class PricingServiceTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 11, 9, 12, 0, 0, TimeSpan.Zero);

    private static readonly Offer Keyboard = new(Guid.NewGuid(), "KEYBOARD-MX", "Mechanical keyboard", true, Eur(79.90m), 0.24m);
    private static readonly Offer Coffee = new(Guid.NewGuid(), "COFFEE-1KG", "Coffee beans 1 kg", true, Eur(18.50m), 0.13m);
    private static readonly Offer Ebook = new(Guid.NewGuid(), "EBOOK-CLEAN", "Clean Architecture (e-book)", false, Eur(24.99m), 0.06m);
    private static readonly Offer Laptop = new(Guid.NewGuid(), "LAPTOP-14", "Laptop 14 inch", true, Eur(899.00m), 0.24m);

    private readonly ICatalogReader _catalog = Substitute.For<ICatalogReader>();
    private readonly ServiceProvider _services;

    public PricingServiceTests()
    {
        _catalog.GetOffersAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(call => Task.FromResult<IReadOnlyList<Offer>>(
                new[] { Keyboard, Coffee, Ebook, Laptop }.Where(offer => call.Arg<IReadOnlyCollection<Guid>>().Contains(offer.ProductId)).ToList()));

        _services = new ServiceCollection()
            .AddSingleton<IConfiguration>(new ConfigurationBuilder().Build())
            .AddSingleton<TimeProvider>(new FakeTimeProvider(Now))
            .AddSingleton(_catalog)
            .AddPricingModule()
            .BuildServiceProvider();
    }

    [Fact]
    public async Task Calculate_PricesEveryLine_WithVatPerLine()
    {
        var quote = await PriceAsync(Line(Keyboard, 2), Line(Coffee, 1));

        var keyboard = quote.Lines[0];
        Assert.Equal(Eur(159.80m), keyboard.NetAmount);
        Assert.Equal(Eur(38.35m), keyboard.TaxAmount);
        var coffee = quote.Lines[1];
        Assert.Equal(Eur(18.50m), coffee.NetAmount);
        Assert.Equal(Eur(2.41m), coffee.TaxAmount);

        Assert.Equal(Eur(178.30m), quote.Subtotal);
        Assert.Equal(Eur(40.76m), quote.Tax);
    }

    [Fact]
    public async Task Calculate_BelowTheThreshold_AddsTheShippingCharge()
    {
        var quote = await PriceAsync(Line(Coffee, 1));

        Assert.Equal(Eur(5.00m), quote.ShippingCharge);
        Assert.Equal(Eur(18.50m + 2.41m + 5.00m), quote.Total);
    }

    [Fact]
    public async Task Calculate_AtTheThreshold_ShipsForFree()
    {
        var offer = Coffee with { ProductId = Guid.NewGuid(), UnitPrice = Eur(100.00m) };
        _catalog.GetOffersAsync(Arg.Is<IReadOnlyCollection<Guid>>(ids => ids.Contains(offer.ProductId)), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<Offer>>([offer]));

        var quote = await PriceAsync(Line(offer, 1));

        Assert.Equal(Eur(0m), quote.ShippingCharge);
    }

    [Fact]
    public async Task Calculate_DigitalOnly_HasNoShippingCharge()
    {
        var quote = await PriceAsync(Line(Ebook, 1));

        Assert.Equal(Eur(0m), quote.ShippingCharge);
        Assert.Equal(Eur(24.99m + 1.50m), quote.Total);
    }

    [Fact]
    public async Task Calculate_TotalIsSubtotalPlusTaxPlusShipping()
    {
        var quote = await PriceAsync(Line(Laptop, 1), Line(Ebook, 2));

        Assert.Equal(quote.Subtotal + quote.Tax + quote.ShippingCharge, quote.Total);
    }

    [Fact]
    public async Task Calculate_AsksTheCatalogForTheCurrentPrices()
    {
        await PriceAsync(Line(Keyboard, 1));

        await _catalog.Received(1).GetOffersAsync(
            Arg.Is<IReadOnlyCollection<Guid>>(ids => ids.Single() == Keyboard.ProductId),
            Now,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Calculate_WithoutLines_Fails()
    {
        Assert.Equal(PricingErrors.NoLines, (await CalculateAsync()).Error);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-2)]
    public async Task Calculate_WithAQuantityNotAboveZero_Fails(int quantity)
    {
        Assert.Equal(PricingErrors.InvalidQuantity, (await CalculateAsync(Line(Keyboard, quantity))).Error);
    }

    [Fact]
    public async Task Calculate_WithTheSameProductTwice_Fails()
    {
        Assert.Equal(PricingErrors.DuplicateProduct, (await CalculateAsync(Line(Keyboard, 1), Line(Keyboard, 2))).Error);
    }

    [Fact]
    public async Task Calculate_WithAProductTheCatalogDoesNotOffer_FailsAndNamesIt()
    {
        var unknown = Guid.NewGuid();

        var result = await CalculateAsync(Line(Keyboard, 1), new PriceRequestLine(unknown, 1));

        Assert.False(result.IsSuccess);
        Assert.Equal("pricing.products_not_available", result.Error!.Code);
        Assert.Contains(unknown.ToString(), result.Error.Message);
    }

    [Fact]
    public async Task Calculate_WithProductsInDifferentCurrencies_Fails()
    {
        var dollars = Keyboard with { ProductId = Guid.NewGuid(), UnitPrice = new Money(10m, Currency.USD) };
        _catalog.GetOffersAsync(Arg.Is<IReadOnlyCollection<Guid>>(ids => ids.Contains(dollars.ProductId)), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<Offer>>([Keyboard, dollars]));

        Assert.Equal(PricingErrors.MixedCurrencies, (await CalculateAsync(Line(Keyboard, 1), Line(dollars, 1))).Error);
    }

    public void Dispose() => _services.Dispose();

    private static PriceRequestLine Line(Offer offer, int quantity) => new(offer.ProductId, quantity);

    private static Money Eur(decimal amount) => new(amount, Currency.EUR);

    private async Task<PriceQuote> PriceAsync(params PriceRequestLine[] lines)
    {
        var result = await CalculateAsync(lines);
        Assert.True(result.IsSuccess, result.Error?.Message);
        return result.Value!;
    }

    private Task<BuildingBlocks.Results.Result<PriceQuote>> CalculateAsync(params PriceRequestLine[] lines)
    {
        using var scope = _services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<IPricingService>().CalculateAsync(new PriceRequest(lines), CancellationToken.None);
    }
}
