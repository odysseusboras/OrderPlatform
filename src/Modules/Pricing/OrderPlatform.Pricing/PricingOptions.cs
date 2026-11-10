namespace OrderPlatform.Pricing;

public sealed class PricingOptions
{
    public const string SectionName = "Pricing";

    /// <summary>Flat charge for orders with physical items, VAT included.</summary>
    public decimal ShippingCharge { get; set; } = 5.00m;

    /// <summary>Net subtotal from which shipping is free.</summary>
    public decimal FreeShippingFrom { get; set; } = 100.00m;
}
