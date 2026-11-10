using OrderPlatform.BuildingBlocks.Money;
using OrderPlatform.BuildingBlocks.Users;

namespace OrderPlatform.Catalog.Persistence;

/// <summary>
/// Reference data for the catalog: Greek VAT categories and the products on offer, with fixed identifiers
/// so that the demo requests and tests can refer to them. Applied through migrations.
/// </summary>
public static class CatalogSeedData
{
    public static readonly DateTimeOffset SeededAt = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    /// <summary>When the seeded price change takes effect (see the keyboard).</summary>
    public static readonly DateTimeOffset PriceChangeAt = new(2026, 6, 1, 0, 0, 0, TimeSpan.Zero);

    public static class TaxCategories
    {
        public static readonly Guid Standard = new("10000000-0000-0000-0000-000000000001");
        public static readonly Guid Reduced = new("10000000-0000-0000-0000-000000000002");
        public static readonly Guid SuperReduced = new("10000000-0000-0000-0000-000000000003");
        public static readonly Guid Zero = new("10000000-0000-0000-0000-000000000004");
    }

    internal static IReadOnlyList<SeedTaxCategory> TaxCategoryRows { get; } =
    [
        new(TaxCategories.Standard, "Standard", 0.24m),
        new(TaxCategories.Reduced, "Reduced", 0.13m),
        new(TaxCategories.SuperReduced, "Super reduced", 0.06m),
        new(TaxCategories.Zero, "Zero", 0m),
    ];

    internal static IReadOnlyList<SeedProduct> ProductRows { get; } =
    [
        new(1, "LAPTOP-14", "Laptop 14 inch", true, TaxCategories.Standard, 899.00m),
        new(2, "MOUSE-WL", "Wireless mouse", true, TaxCategories.Standard, 24.90m),
        new(3, "KEYBOARD-MX", "Mechanical keyboard", true, TaxCategories.Standard, 79.90m, PreviousPrice: 89.90m),
        new(4, "MONITOR-27", "Monitor 27 inch", true, TaxCategories.Standard, 249.00m),
        new(5, "BOOK-DDD", "Domain-Driven Design (book)", true, TaxCategories.SuperReduced, 54.00m),
        new(6, "COFFEE-1KG", "Coffee beans 1 kg", true, TaxCategories.Reduced, 18.50m),
        new(7, "EBOOK-CLEAN", "Clean Architecture (e-book)", false, TaxCategories.SuperReduced, 24.99m),
        new(8, "IDE-LICENCE-1Y", "IDE licence, 1 year", false, TaxCategories.Standard, 149.00m),
        new(9, "GIFT-CARD-50", "Gift card 50 EUR", false, TaxCategories.Zero, 50.00m),
    ];

    internal static IEnumerable<object> TaxCategoryData() =>
        TaxCategoryRows.Select(row => new
        {
            row.Id,
            row.Name,
            row.Rate,
            CreatedAt = SeededAt,
            CreatedBy = SystemUser.Id,
        });

    internal static IEnumerable<object> ProductData() =>
        ProductRows.Select(row => new
        {
            row.Id,
            row.Sku,
            row.Name,
            row.IsPhysical,
            IsActive = true,
            row.TaxCategoryId,
            CreatedAt = SeededAt,
            CreatedBy = SystemUser.Id,
        });

    internal static IEnumerable<object> ProductPriceData()
    {
        foreach (var row in ProductRows)
        {
            if (row.PreviousPrice is { } previous)
            {
                yield return Price(row.PreviousPriceId, row.Id, previous, SeededAt, PriceChangeAt);
                yield return Price(row.PriceId, row.Id, row.Price, PriceChangeAt, null);
            }
            else
            {
                yield return Price(row.PriceId, row.Id, row.Price, SeededAt, null);
            }
        }
    }

    private static object Price(Guid id, Guid productId, decimal amount, DateTimeOffset validFrom, DateTimeOffset? validTo) => new
    {
        Id = id,
        ProductId = productId,
        Amount = amount,
        Currency = Currency.EUR,
        ValidFrom = validFrom,
        ValidTo = validTo,
        CreatedAt = SeededAt,
        CreatedBy = SystemUser.Id,
    };

    internal sealed record SeedTaxCategory(Guid Id, string Name, decimal Rate);

    internal sealed record SeedProduct(int Number, string Sku, string Name, bool IsPhysical, Guid TaxCategoryId, decimal Price, decimal? PreviousPrice = null)
    {
        public Guid Id => new($"20000000-0000-0000-0000-{Number:D12}");

        public Guid PriceId => new($"30000000-0000-0000-0000-{Number:D12}");

        public Guid PreviousPriceId => new($"30000000-0000-0000-0001-{Number:D12}");
    }
}
