using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OrderPlatform.Pricing.Contracts;
using OrderPlatform.Pricing.Rules;

namespace OrderPlatform.Pricing;

public static class PricingServiceCollectionExtensions
{
    /// <summary>
    /// Registers the pricing service and its rules. The rules run in the order registered here:
    /// VAT first, because the shipping threshold looks at the priced lines.
    /// Needs <c>ICatalogReader</c> from the catalog module.
    /// </summary>
    public static IServiceCollection AddPricingModule(this IServiceCollection services)
    {
        services.AddOptions<PricingOptions>()
            .BindConfiguration(PricingOptions.SectionName)
            .Validate(options => options.ShippingCharge >= 0, "Pricing:ShippingCharge cannot be negative.")
            .Validate(options => options.FreeShippingFrom >= 0, "Pricing:FreeShippingFrom cannot be negative.")
            .ValidateOnStart();

        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<IPricingRule, VatRule>();
        services.AddSingleton<IPricingRule, ShippingChargeRule>();
        services.AddScoped<IPricingService, PricingService>();

        return services;
    }
}
