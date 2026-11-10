using OrderPlatform.BuildingBlocks.Results;
using OrderPlatform.Catalog.Contracts;
using OrderPlatform.Pricing.Calculation;
using OrderPlatform.Pricing.Contracts;
using OrderPlatform.Pricing.Rules;

namespace OrderPlatform.Pricing;

internal sealed class PricingService(
    ICatalogReader catalog,
    IEnumerable<IPricingRule> rules,
    TimeProvider timeProvider) : IPricingService
{
    public async Task<Result<PriceQuote>> CalculateAsync(PriceRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (Validate(request) is { } invalid)
        {
            return Result<PriceQuote>.Failure(invalid);
        }

        var productIds = request.Lines.Select(line => line.ProductId).ToList();
        var offers = (await catalog.GetOffersAsync(productIds, timeProvider.GetUtcNow(), cancellationToken))
            .ToDictionary(offer => offer.ProductId);

        var missing = productIds.Where(id => !offers.ContainsKey(id)).ToList();
        if (missing.Count > 0)
        {
            return Result<PriceQuote>.Failure(PricingErrors.ProductsNotAvailable(missing));
        }

        var currencies = offers.Values.Select(offer => offer.UnitPrice.Currency).Distinct().ToList();
        if (currencies.Count > 1)
        {
            return Result<PriceQuote>.Failure(PricingErrors.MixedCurrencies);
        }

        var calculation = new PriceCalculation(currencies[0], request.Lines.Select(line => ToPricedLine(line, offers[line.ProductId])).ToList());
        foreach (var rule in rules)
        {
            rule.Apply(calculation);
        }

        return Result<PriceQuote>.Success(ToQuote(calculation));
    }

    private static Error? Validate(PriceRequest request)
    {
        if (request.Lines is not { Count: > 0 })
        {
            return PricingErrors.NoLines;
        }

        if (request.Lines.Any(line => line.Quantity <= 0))
        {
            return PricingErrors.InvalidQuantity;
        }

        return request.Lines.DistinctBy(line => line.ProductId).Count() != request.Lines.Count
            ? PricingErrors.DuplicateProduct
            : null;
    }

    private static PricedLine ToPricedLine(PriceRequestLine line, Offer offer) =>
        new(offer.ProductId, offer.Sku, line.Quantity, offer.IsPhysical, offer.UnitPrice, offer.TaxRate);

    private static PriceQuote ToQuote(PriceCalculation calculation) =>
        new(
            calculation.Lines
                .Select(line => new PriceQuoteLine(
                    line.ProductId,
                    line.Sku,
                    line.Quantity,
                    line.IsPhysical,
                    line.UnitPrice,
                    line.NetAmount,
                    line.TaxRate,
                    line.TaxAmount))
                .ToList(),
            calculation.Subtotal,
            calculation.Tax,
            calculation.ShippingCharge,
            calculation.Total);
}
