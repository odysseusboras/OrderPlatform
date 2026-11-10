using OrderPlatform.BuildingBlocks.Money;

namespace OrderPlatform.Pricing.Contracts;

public sealed record PriceQuote(
    IReadOnlyList<PriceQuoteLine> Lines,
    Money Subtotal,
    Money Tax,
    Money ShippingCharge,
    Money Total);

/// <summary>One priced line. <see cref="NetAmount"/> is without VAT; <see cref="TaxAmount"/> is rounded per line.</summary>
public sealed record PriceQuoteLine(
    Guid ProductId,
    string Sku,
    int Quantity,
    bool IsPhysical,
    Money UnitPrice,
    Money NetAmount,
    decimal TaxRate,
    Money TaxAmount);
