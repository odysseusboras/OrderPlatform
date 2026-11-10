using OrderPlatform.BuildingBlocks.Paging;

namespace OrderPlatform.Catalog.Contracts;

/// <summary>Read access to what can be bought, and at which price, at a given moment.</summary>
public interface ICatalogReader
{
    /// <summary>One page of the active products that have a price at <paramref name="at"/>, ordered by SKU.</summary>
    Task<Page<Offer>> GetOffersAsync(PageRequest page, DateTimeOffset at, CancellationToken cancellationToken);

    /// <summary>The offers for the given products. Products that are unknown, inactive or without a price are left out.</summary>
    Task<IReadOnlyList<Offer>> GetOffersAsync(IReadOnlyCollection<Guid> productIds, DateTimeOffset at, CancellationToken cancellationToken);
}
