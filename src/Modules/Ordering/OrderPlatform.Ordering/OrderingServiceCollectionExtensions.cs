using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OrderPlatform.Ordering.Fulfilment;
using OrderPlatform.Ordering.Orders;
using OrderPlatform.Ordering.Persistence;
using OrderPlatform.Ordering.Placing;
using OrderPlatform.ProcessEngine;
using OrderPlatform.ProcessEngine.Execution;

namespace OrderPlatform.Ordering;

public static class OrderingServiceCollectionExtensions
{
    /// <summary>
    /// Registers the order use cases and the fulfilment pipeline. Needs the shared <c>DbContext</c>,
    /// <c>ICatalogReader</c>, <c>IPricingService</c> and the gateways of the external systems.
    /// </summary>
    public static IServiceCollection AddOrderingModule(this IServiceCollection services)
    {
        var pipeline = new OrderFulfilmentPipelineV1();
        services.AddSingleton(pipeline);
        services.AddPipeline(pipeline);

        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<IOrderRepository, OrderRepository>();
        services.AddScoped<IOrderService, OrderService>();
        services.AddScoped<IPipelineObserver<OrderFulfilmentContext>, OrderProgressObserver>();

        return services;
    }
}
