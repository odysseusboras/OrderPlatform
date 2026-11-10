using OrderPlatform.BuildingBlocks.Money;
using OrderPlatform.Catalog.Contracts;

namespace OrderPlatform.Api.Models;

/// <summary>A product on sale. Prices are per unit; the gross price includes VAT.</summary>
public sealed record OfferResponse(
    Guid ProductId,
    string Sku,
    string Name,
    bool IsPhysical,
    Currency Currency,
    decimal NetPrice,
    decimal VatRate,
    decimal VatAmount,
    decimal GrossPrice)
{
    public static OfferResponse From(Offer offer)
    {
        var vat = offer.UnitPrice.PercentOf(offer.TaxRate);

        return new OfferResponse(
            offer.ProductId,
            offer.Sku,
            offer.Name,
            offer.IsPhysical,
            offer.UnitPrice.Currency,
            offer.UnitPrice.Amount,
            offer.TaxRate,
            vat.Amount,
            (offer.UnitPrice + vat).Amount);
    }
}
