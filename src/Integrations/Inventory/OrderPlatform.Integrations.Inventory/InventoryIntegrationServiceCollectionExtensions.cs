using Microsoft.Extensions.DependencyInjection;
using OrderPlatform.Integrations.Inventory.Contracts;

namespace OrderPlatform.Integrations.Inventory;

public static class InventoryIntegrationServiceCollectionExtensions
{
    public static IServiceCollection AddInventoryIntegration(this IServiceCollection services) =>
        services.AddScoped<IInventoryGateway, InventoryGateway>();
}
