using OrderPlatform.BuildingBlocks.Money;

namespace OrderPlatform.Pricing.Calculation;

/// <summary>The working state that pricing rules read and complete, one rule after another.</summary>
internal sealed class PriceCalculation(Currency currency, IReadOnlyList<PricedLine> lines)
{
    public Currency Currency { get; } = currency;

    public IReadOnlyList<PricedLine> Lines { get; } = lines;

    public Money ShippingCharge { get; set; } = Money.Zero(currency);

    public bool HasPhysicalItems => Lines.Any(line => line.IsPhysical);

    public Money Subtotal => Lines.Aggregate(Money.Zero(Currency), (sum, line) => sum + line.NetAmount);

    public Money Tax => Lines.Aggregate(Money.Zero(Currency), (sum, line) => sum + line.TaxAmount);

    public Money Total => Subtotal + Tax + ShippingCharge;
}

internal sealed class PricedLine(Guid productId, string sku, int quantity, bool isPhysical, Money unitPrice, decimal taxRate)
{
    public Guid ProductId { get; } = productId;

    public string Sku { get; } = sku;

    public int Quantity { get; } = quantity;

    public bool IsPhysical { get; } = isPhysical;

    public Money UnitPrice { get; } = unitPrice;

    public decimal TaxRate { get; } = taxRate;

    public Money NetAmount => UnitPrice * Quantity;

    public Money TaxAmount { get; set; } = Money.Zero(unitPrice.Currency);
}
