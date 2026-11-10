using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NSubstitute;
using OrderPlatform.Catalog.Contracts;
using OrderPlatform.Pricing.Contracts;

namespace OrderPlatform.Pricing.Tests;

public sealed class PricingServiceRegistrationTests
{
    [Fact]
    public void AddPricingModule_RegistersThePricingService()
    {
        using var provider = Services().BuildServiceProvider();
        using var scope = provider.CreateScope();

        Assert.NotNull(scope.ServiceProvider.GetService<IPricingService>());
    }

    [Fact]
    public void AddPricingModule_BindsOptionsFromThePricingSection()
    {
        using var provider = Services(("Pricing:ShippingCharge", "7.50"), ("Pricing:FreeShippingFrom", "50")).BuildServiceProvider();

        var options = provider.GetRequiredService<IOptions<PricingOptions>>().Value;

        Assert.Equal(7.50m, options.ShippingCharge);
        Assert.Equal(50m, options.FreeShippingFrom);
    }

    [Fact]
    public void AddPricingModule_RejectsANegativeShippingCharge()
    {
        using var provider = Services(("Pricing:ShippingCharge", "-1")).BuildServiceProvider();

        Assert.Throws<OptionsValidationException>(() => provider.GetRequiredService<IOptions<PricingOptions>>().Value);
    }

    private static IServiceCollection Services(params (string Key, string Value)[] settings)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(settings.Select(setting => new KeyValuePair<string, string?>(setting.Key, setting.Value)))
            .Build();

        return new ServiceCollection()
            .AddSingleton<IConfiguration>(configuration)
            .AddSingleton(Substitute.For<ICatalogReader>())
            .AddPricingModule();
    }
}
