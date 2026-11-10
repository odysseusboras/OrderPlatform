using System.Linq.Expressions;
using OrderPlatform.BuildingBlocks.Auditing;
using OrderPlatform.BuildingBlocks.Money;

namespace OrderPlatform.Catalog.Products;

/// <summary>A net price (without VAT) valid for a period. Old prices are kept as history.</summary>
public sealed class ProductPrice : AuditableEntity
{
    private ProductPrice()
    {
    }

    internal ProductPrice(Guid productId, Money price, DateTimeOffset validFrom)
    {
        ProductId = productId;
        Amount = price.Amount;
        Currency = price.Currency;
        ValidFrom = validFrom;
    }

    public Guid ProductId { get; private set; }

    public decimal Amount { get; private set; }

    public Currency Currency { get; private set; }

    public DateTimeOffset ValidFrom { get; private set; }

    public DateTimeOffset? ValidTo { get; private set; }

    public Money Price => new(Amount, Currency);

    public bool IsValidAt(DateTimeOffset at) => ValidFrom <= at && (ValidTo is null || at < ValidTo);

    /// <summary>The same rule as <see cref="IsValidAt"/>, for database queries. Both are covered by the same tests.</summary>
    public static Expression<Func<ProductPrice, bool>> ValidAt(DateTimeOffset at) =>
        price => price.ValidFrom <= at && (price.ValidTo == null || at < price.ValidTo);

    internal void Close(DateTimeOffset at) => ValidTo = at;
}
