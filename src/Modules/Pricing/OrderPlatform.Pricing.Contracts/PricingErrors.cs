using OrderPlatform.BuildingBlocks.Results;

namespace OrderPlatform.Pricing.Contracts;

public static class PricingErrors
{
    public static readonly Error NoLines = new("pricing.no_lines", "At least one product is needed to calculate a price.");

    public static readonly Error InvalidQuantity = new("pricing.invalid_quantity", "Every quantity must be greater than zero.");

    public static readonly Error DuplicateProduct = new("pricing.duplicate_product", "A product can appear only once; use the quantity instead.");

    public static readonly Error MixedCurrencies = new("pricing.mixed_currencies", "The products are priced in different currencies.");

    public static Error ProductsNotAvailable(IEnumerable<Guid> productIds) =>
        new("pricing.products_not_available", $"These products are unknown, inactive or have no price: {string.Join(", ", productIds)}.");
}
