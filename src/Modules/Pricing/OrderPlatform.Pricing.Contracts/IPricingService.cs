using OrderPlatform.BuildingBlocks.Results;

namespace OrderPlatform.Pricing.Contracts;

public interface IPricingService
{
    /// <summary>
    /// Prices the requested products at their current catalog price: VAT per line, then additional charges.
    /// Fails with one of the <see cref="PricingErrors"/> when the request cannot be priced.
    /// </summary>
    Task<Result<PriceQuote>> CalculateAsync(PriceRequest request, CancellationToken cancellationToken);
}
