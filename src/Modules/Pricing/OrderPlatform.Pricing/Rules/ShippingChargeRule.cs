using Microsoft.Extensions.Options;
using OrderPlatform.BuildingBlocks.Money;
using OrderPlatform.Pricing.Calculation;

namespace OrderPlatform.Pricing.Rules;

/// <summary>A flat shipping charge for physical items, free from a net subtotal threshold.</summary>
internal sealed class ShippingChargeRule(IOptions<PricingOptions> options) : IPricingRule
{
    public void Apply(PriceCalculation calculation)
    {
        var settings = options.Value;
        var isFree = !calculation.HasPhysicalItems || calculation.Subtotal.Amount >= settings.FreeShippingFrom;

        calculation.ShippingCharge = isFree
            ? Money.Zero(calculation.Currency)
            : new Money(settings.ShippingCharge, calculation.Currency);
    }
}
