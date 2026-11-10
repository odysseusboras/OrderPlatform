using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OrderPlatform.BuildingBlocks.Money;
using OrderPlatform.BuildingBlocks.Paging;
using OrderPlatform.Catalog.Contracts;
using OrderPlatform.Catalog.Persistence;
using OrderPlatform.Catalog.Products;
using OrderPlatform.TestUtilities.Database;

namespace OrderPlatform.Catalog.Tests.Offers;

/// <summary>
/// Runs the catalog's own configurations and seed data on an in-memory SQLite database.
/// SQL Server specifics (temporal tables, schemas) are covered by the persistence tests.
/// </summary>
public sealed class CatalogReaderTests : IDisposable
{
    private const int SeededProducts = 9;

    private static readonly DateTimeOffset BeforeSeed = CatalogSeedData.SeededAt.AddDays(-1);
    private static readonly DateTimeOffset BeforePriceChange = CatalogSeedData.PriceChangeAt.AddDays(-1);
    private static readonly DateTimeOffset AfterPriceChange = CatalogSeedData.PriceChangeAt.AddDays(1);
    private static readonly Guid Keyboard = new("20000000-0000-0000-0000-000000000003");
    private static readonly Guid Ebook = new("20000000-0000-0000-0000-000000000007");

    private readonly SqliteTestDatabase _database = new(typeof(Product).Assembly);
    private readonly ServiceProvider _services;

    public CatalogReaderTests()
    {
        _services = _database.Register(new ServiceCollection()).AddCatalogModule().BuildServiceProvider();
        SqliteTestDatabase.Create(_services);
    }

    [Fact]
    public async Task GetOffers_FirstPage_ReturnsThePageOrderedBySku_AndTheTotal()
    {
        var page = await Reader().GetOffersAsync(new PageRequest(1, 4), AfterPriceChange, CancellationToken.None);

        Assert.Equal(4, page.Items.Count);
        Assert.Equal(SeededProducts, page.TotalCount);
        Assert.Equal(3, page.TotalPages);
        Assert.Equal(page.Items.Select(offer => offer.Sku).Order(StringComparer.Ordinal), page.Items.Select(offer => offer.Sku));
    }

    [Fact]
    public async Task GetOffers_AllPages_ReturnEveryProductExactlyOnce()
    {
        var skus = new List<string>();
        for (var pageNumber = 1; pageNumber <= 3; pageNumber++)
        {
            var page = await Reader().GetOffersAsync(new PageRequest(pageNumber, 4), AfterPriceChange, CancellationToken.None);
            skus.AddRange(page.Items.Select(offer => offer.Sku));
        }

        Assert.Equal(SeededProducts, skus.Distinct().Count());
        Assert.Equal(skus.Order(StringComparer.Ordinal), skus);
    }

    [Fact]
    public async Task GetOffers_PageAfterTheLast_IsEmpty()
    {
        var page = await Reader().GetOffersAsync(new PageRequest(10, 4), AfterPriceChange, CancellationToken.None);

        Assert.Empty(page.Items);
        Assert.Equal(SeededProducts, page.TotalCount);
    }

    [Fact]
    public async Task GetOffers_UsesThePriceValidAtTheRequestedMoment()
    {
        var before = await Reader().GetOffersAsync([Keyboard], BeforePriceChange, CancellationToken.None);
        var after = await Reader().GetOffersAsync([Keyboard], AfterPriceChange, CancellationToken.None);

        Assert.Equal(new Money(89.90m, Currency.EUR), Assert.Single(before).UnitPrice);
        Assert.Equal(new Money(79.90m, Currency.EUR), Assert.Single(after).UnitPrice);
    }

    [Fact]
    public async Task GetOffers_CarriesTheTaxRateAndWhetherTheProductIsPhysical()
    {
        var ebook = Assert.Single(await Reader().GetOffersAsync([Ebook], AfterPriceChange, CancellationToken.None));

        Assert.Equal(0.06m, ebook.TaxRate);
        Assert.False(ebook.IsPhysical);
    }

    [Fact]
    public async Task GetOffers_BeforeAnyPrice_IsEmptyAndCountsNothing()
    {
        var page = await Reader().GetOffersAsync(new PageRequest(1, 20), BeforeSeed, CancellationToken.None);

        Assert.Empty(page.Items);
        Assert.Equal(0, page.TotalCount);
    }

    [Fact]
    public async Task GetOffers_LeavesOutInactiveProducts_FromItemsAndTotal()
    {
        using (var scope = _services.CreateScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<DbContext>();
            var keyboard = await database.Set<Product>().SingleAsync(product => product.Id == Keyboard);
            keyboard.Deactivate();
            await database.SaveChangesAsync();
        }

        var page = await Reader().GetOffersAsync(new PageRequest(1, 20), AfterPriceChange, CancellationToken.None);

        Assert.DoesNotContain(page.Items, offer => offer.ProductId == Keyboard);
        Assert.Equal(SeededProducts - 1, page.TotalCount);
    }

    [Fact]
    public async Task GetOffers_ForSelectedProducts_IgnoresUnknownIds()
    {
        var offers = await Reader().GetOffersAsync([Keyboard, Guid.NewGuid()], AfterPriceChange, CancellationToken.None);

        Assert.Equal(Keyboard, Assert.Single(offers).ProductId);
    }

    public void Dispose()
    {
        _services.Dispose();
        _database.Dispose();
    }

    private ICatalogReader Reader() => _services.CreateScope().ServiceProvider.GetRequiredService<ICatalogReader>();
}
