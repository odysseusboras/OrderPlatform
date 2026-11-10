using OrderPlatform.Pricing.Calculation;

namespace OrderPlatform.Pricing.Rules;

/// <summary>VAT per line at the product's rate, rounded per line.</summary>
internal sealed class VatRule : IPricingRule
{
    public void Apply(PriceCalculation calculation)
    {
        foreach (var line in calculation.Lines)
        {
            line.TaxAmount = line.NetAmount.PercentOf(line.TaxRate);
        }
    }
}
