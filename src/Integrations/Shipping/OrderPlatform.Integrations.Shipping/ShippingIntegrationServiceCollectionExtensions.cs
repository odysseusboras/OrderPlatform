using Microsoft.Extensions.DependencyInjection;
using OrderPlatform.Integrations.Shipping.Contracts;

namespace OrderPlatform.Integrations.Shipping;

public static class ShippingIntegrationServiceCollectionExtensions
{
    public static IServiceCollection AddShippingIntegration(this IServiceCollection services) =>
        services.AddScoped<IShippingGateway, ShippingGateway>();
}
