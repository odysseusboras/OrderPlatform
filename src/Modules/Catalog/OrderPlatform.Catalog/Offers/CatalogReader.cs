using Microsoft.EntityFrameworkCore;
using OrderPlatform.BuildingBlocks.Money;
using OrderPlatform.BuildingBlocks.Paging;
using OrderPlatform.Catalog.Contracts;
using OrderPlatform.Catalog.Products;

namespace OrderPlatform.Catalog.Offers;

/// <summary>
/// Reads offers through the shared database context. Filtering, ordering and paging run in the database,
/// so the cost of a request does not grow with the size of the catalog.
/// </summary>
internal sealed class CatalogReader(DbContext database) : ICatalogReader
{
    public async Task<Page<Offer>> GetOffersAsync(PageRequest page, DateTimeOffset at, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(page);

        var offers = OffersAt(database.Set<Product>(), at);
        var totalCount = await offers.CountAsync(cancellationToken);
        var items = await offers
            .Skip(page.Skip)
            .Take(page.PageSize)
            .ToListAsync(cancellationToken);

        return new Page<Offer>(items.Select(ToOffer).ToList(), page.PageNumber, page.PageSize, totalCount);
    }

    public async Task<IReadOnlyList<Offer>> GetOffersAsync(
        IReadOnlyCollection<Guid> productIds,
        DateTimeOffset at,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(productIds);

        var products = database.Set<Product>().Where(product => productIds.Contains(product.Id));
        var items = await OffersAt(products, at).ToListAsync(cancellationToken);

        return items.Select(ToOffer).ToList();
    }

    /// <summary>Active products with a valid price, joined to their VAT rate and ordered by SKU.</summary>
    private IQueryable<OfferRow> OffersAt(IQueryable<Product> products, DateTimeOffset at)
    {
        var validAt = ProductPrice.ValidAt(at);

        return from product in products.AsNoTracking()
               where product.IsActive && product.Prices.AsQueryable().Any(validAt)
               join category in database.Set<TaxCategory>() on product.TaxCategoryId equals category.Id
               let price = product.Prices.AsQueryable().Where(validAt).OrderByDescending(candidate => candidate.ValidFrom).First()
               orderby product.Sku
               select new OfferRow(product.Id, product.Sku, product.Name, product.IsPhysical, price.Amount, price.Currency, category.Rate);
    }

    private static Offer ToOffer(OfferRow row) =>
        new(row.ProductId, row.Sku, row.Name, row.IsPhysical, new Money(row.Amount, row.Currency), row.TaxRate);

    private sealed record OfferRow(Guid ProductId, string Sku, string Name, bool IsPhysical, decimal Amount, Currency Currency, decimal TaxRate);
}
