using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using OrderPlatform.BuildingBlocks.Results;
using OrderPlatform.Catalog.Contracts;
using OrderPlatform.FakeExternalSystems;
using OrderPlatform.FakeExternalSystems.Inventory;
using OrderPlatform.Integrations.Inventory.Contracts;
using OrderPlatform.Ordering.Fulfilment;
using OrderPlatform.Ordering.Orders;
using OrderPlatform.Pricing.Contracts;
using OrderPlatform.ProcessEngine.Execution;
using OrderPlatform.ProcessEngine.Runs;
using OrderPlatform.TestUtilities.Database;

namespace OrderPlatform.Ordering.Tests.Support;

/// <summary>
/// The Ordering module with the real process engine on an in-memory SQLite database.
/// The catalog and pricing are substitutes and the inventory is the fake; every call runs in its own unit of work, as an HTTP request would.
/// </summary>
internal sealed class OrderingHarness : IDisposable
{
    private readonly SqliteTestDatabase _database = new(typeof(Order).Assembly, typeof(PipelineRun).Assembly);
    private readonly ServiceProvider _services;

    public OrderingHarness()
    {
        List<Offer> offers = [OrderingTestData.Keyboard, OrderingTestData.Ebook, OrderingTestData.DollarMug];
        Catalog.GetOffersAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(call => (IReadOnlyList<Offer>)offers.Where(offer => call.Arg<IReadOnlyCollection<Guid>>().Contains(offer.ProductId)).ToList());
        PriceSucceeds(OrderingTestData.KeyboardPrice);

        _services = _database.Register(new ServiceCollection())
            .AddSingleton<TimeProvider>(Clock)
            .AddSingleton(typeof(ILogger<>), typeof(NullLogger<>))
            .AddSingleton(Catalog)
            .AddSingleton(Pricing)
            .AddFakeExternalSystems(fakes => fakes.Inventory = Inventory)
            .AddOrderingModule()
            .BuildServiceProvider();
        SqliteTestDatabase.Create(_services);
    }

    public FakeTimeProvider Clock { get; } = new(new DateTimeOffset(2026, 11, 10, 9, 0, 0, TimeSpan.Zero));

    public ICatalogReader Catalog { get; } = Substitute.For<ICatalogReader>();

    public IPricingService Pricing { get; } = Substitute.For<IPricingService>();

    /// <summary>Failure scenarios of the fake inventory; change them at any time during a test.</summary>
    public FakeInventoryOptions Inventory { get; } = new();

    public bool IsReserved(string reservationId) =>
        ((FakeInventoryGateway)_services.GetRequiredService<IInventoryGateway>()).IsReserved(reservationId);

    public void PriceSucceeds(PriceSnapshot price) =>
        Pricing.CalculateAsync(Arg.Any<PriceRequest>(), Arg.Any<CancellationToken>())
            .Returns(Result<PriceQuote>.Success(new PriceQuote([], price.Subtotal, price.Tax, price.ShippingCharge, price.Total)));

    public void PriceFails(Error error) =>
        Pricing.CalculateAsync(Arg.Any<PriceRequest>(), Arg.Any<CancellationToken>()).Returns(Result<PriceQuote>.Failure(error));

    /// <summary>The background worker over this harness; call RunOnceAsync to do what it does on every poll.</summary>
    public PipelineWorker Worker() =>
        new(_services.GetRequiredService<IServiceScopeFactory>(), Clock, NullLogger<PipelineWorker>.Instance);

    public async Task<T> InScopeAsync<T>(Func<IServiceProvider, Task<T>> action)
    {
        await using var scope = _services.CreateAsyncScope();
        return await action(scope.ServiceProvider);
    }

    public void Dispose()
    {
        _services.Dispose();
        _database.Dispose();
    }
}
