using OrderPlatform.Pricing.Calculation;

namespace OrderPlatform.Pricing.Rules;

/// <summary>
/// One step of the price calculation. Rules run in registration order; a new charge or discount is a new rule.
/// </summary>
internal interface IPricingRule
{
    void Apply(PriceCalculation calculation);
}
