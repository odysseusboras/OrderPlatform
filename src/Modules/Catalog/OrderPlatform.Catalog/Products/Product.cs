using OrderPlatform.BuildingBlocks.Auditing;
using OrderPlatform.BuildingBlocks.Money;

namespace OrderPlatform.Catalog.Products;

public sealed class Product : AuditableEntity
{
    private readonly List<ProductPrice> _prices = [];

    private Product()
    {
    }

    public string Sku { get; private set; } = string.Empty;

    public string Name { get; private set; } = string.Empty;

    /// <summary>False for digital products: they skip the stock and shipping steps.</summary>
    public bool IsPhysical { get; private set; }

    public bool IsActive { get; private set; }

    public Guid TaxCategoryId { get; private set; }

    public IReadOnlyList<ProductPrice> Prices => _prices;

    public static Product Create(string sku, string name, bool isPhysical, Guid taxCategoryId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sku);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (taxCategoryId == Guid.Empty)
        {
            throw new ArgumentException("A product needs a tax category.", nameof(taxCategoryId));
        }

        return new Product
        {
            Sku = sku.Trim().ToUpperInvariant(),
            Name = name.Trim(),
            IsPhysical = isPhysical,
            IsActive = true,
            TaxCategoryId = taxCategoryId,
        };
    }

    /// <summary>Sets a new net price from <paramref name="validFrom"/> and closes the price that was valid until then.</summary>
    public void SetPrice(Money price, DateTimeOffset validFrom)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(price.Amount);
        if (!Enum.IsDefined(price.Currency))
        {
            throw new ArgumentOutOfRangeException(nameof(price), price.Currency, "Unknown currency.");
        }

        var latest = _prices.MaxBy(existing => existing.ValidFrom);
        if (latest is not null && validFrom <= latest.ValidFrom)
        {
            throw new InvalidOperationException("A new price must start after the latest price.");
        }

        latest?.Close(validFrom);
        _prices.Add(new ProductPrice(Id, price, validFrom));
    }

    public Money? PriceAt(DateTimeOffset at) => _prices.SingleOrDefault(price => price.IsValidAt(at))?.Price;

    public void Deactivate() => IsActive = false;
}
