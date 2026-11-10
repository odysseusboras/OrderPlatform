using OrderPlatform.BuildingBlocks.Auditing;

namespace OrderPlatform.Catalog.Products;

/// <summary>A VAT category, for example Standard 24% or Reduced 13%.</summary>
public sealed class TaxCategory : AuditableEntity
{
    private TaxCategory()
    {
    }

    public string Name { get; private set; } = string.Empty;

    /// <summary>The VAT rate as a fraction: 0.24 means 24%.</summary>
    public decimal Rate { get; private set; }

    public static TaxCategory Create(string name, decimal rate)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentOutOfRangeException.ThrowIfNegative(rate);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(rate, 1m);

        return new TaxCategory { Name = name.Trim(), Rate = rate };
    }
}
