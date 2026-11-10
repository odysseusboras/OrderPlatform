using OrderPlatform.BuildingBlocks.Money;

namespace OrderPlatform.Catalog.Contracts;

/// <summary>An active product with the net price (without VAT) valid at the requested moment.</summary>
public sealed record Offer(Guid ProductId, string Sku, string Name, bool IsPhysical, Money UnitPrice, decimal TaxRate);
