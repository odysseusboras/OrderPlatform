using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using OrderPlatform.Catalog.Contracts;
using OrderPlatform.FakeExternalSystems;
using OrderPlatform.Integrations.Inventory.Contracts;
using OrderPlatform.Integrations.Payments.Contracts;
using OrderPlatform.Integrations.Shipping.Contracts;
using OrderPlatform.Ordering.Fulfilment;
using OrderPlatform.Ordering.Placing;
using OrderPlatform.Pricing.Contracts;
using OrderPlatform.ProcessEngine.Execution;

namespace OrderPlatform.Ordering.Tests;

public sealed class OrderingServiceRegistrationTests
{
    [Fact]
    public void AddOrderingModule_RegistersThePipeline()
    {
        using var provider = new ServiceCollection().AddOrderingModule().BuildServiceProvider();

        Assert.NotNull(provider.GetService<OrderFulfilmentPipelineV1>());
    }

    [Fact]
    public void AddOrderingModule_RegistersTheOrderService_TheObserver_AndEveryStep_WithTheirDependencies()
    {
        var services = new ServiceCollection()
            .AddOrderingModule()
            .AddSingleton(typeof(ILogger<>), typeof(NullLogger<>))
            .AddScoped(_ => Substitute.For<DbContext>())
            .AddSingleton(Substitute.For<ICatalogReader>())
            .AddSingleton(Substitute.For<IPricingService>())
            .AddSingleton(Substitute.For<IInventoryGateway>())
            .AddSingleton(Substitute.For<IPaymentGateway>())
            .AddSingleton(Substitute.For<IShippingGateway>());
        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
        using var scope = provider.CreateScope();

        Assert.NotNull(scope.ServiceProvider.GetService<IOrderService>());
        Assert.Single(scope.ServiceProvider.GetServices<IPipelineObserver<OrderFulfilmentContext>>());
        Assert.All(
            new OrderFulfilmentPipelineV1().Steps,
            step => Assert.NotNull(scope.ServiceProvider.GetService(step.StepType)));
    }

    [Fact]
    public void AddOrderingModule_RegistersAnExecutorForTheFulfilmentPipeline()
    {
        using var provider = new ServiceCollection()
            .AddOrderingModule()
            .AddSingleton(typeof(ILogger<>), typeof(NullLogger<>))
            .AddScoped(_ => Substitute.For<DbContext>())
            .BuildServiceProvider();
        using var scope = provider.CreateScope();

        var executor = Assert.Single(scope.ServiceProvider.GetServices<IPipelineExecutor>());
        Assert.Equal(new OrderFulfilmentPipelineV1().Name, executor.PipelineName);
    }

    [Fact]
    public void AddFakeExternalSystems_RegistersAFakeForEveryGateway()
    {
        using var provider = new ServiceCollection().AddFakeExternalSystems().BuildServiceProvider();

        Assert.All(
            new object?[]
            {
                provider.GetService<IInventoryGateway>(),
                provider.GetService<IPaymentGateway>(),
                provider.GetService<IShippingGateway>(),
            },
            gateway => Assert.StartsWith("OrderPlatform.FakeExternalSystems", gateway?.GetType().Namespace));
    }
}
