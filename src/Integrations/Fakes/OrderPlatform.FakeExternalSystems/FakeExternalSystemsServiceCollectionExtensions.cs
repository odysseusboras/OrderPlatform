using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OrderPlatform.FakeExternalSystems.Inventory;
using OrderPlatform.FakeExternalSystems.Payments;
using OrderPlatform.FakeExternalSystems.Shipping;
using OrderPlatform.Integrations.Inventory.Contracts;
using OrderPlatform.Integrations.Payments.Contracts;
using OrderPlatform.Integrations.Shipping.Contracts;

namespace OrderPlatform.FakeExternalSystems;

public static class FakeExternalSystemsServiceCollectionExtensions
{
    /// <summary>
    /// Replaces the real external systems with in-memory fakes. Development and tests only.
    /// <paramref name="configure"/> sets the failure scenarios (for example which SKUs are out of stock).
    /// </summary>
    public static IServiceCollection AddFakeExternalSystems(this IServiceCollection services, Action<FakeExternalSystemsOptions>? configure = null)
    {
        var options = new FakeExternalSystemsOptions();
        configure?.Invoke(options);

        services.TryAddSingleton(TimeProvider.System);
        return services
            .AddSingleton(options)
            .AddSingleton<IInventoryGateway, FakeInventoryGateway>()
            .AddSingleton<IPaymentGateway, FakePaymentGateway>()
            .AddSingleton<IShippingGateway, FakeShippingGateway>();
    }
}
