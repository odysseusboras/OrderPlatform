using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using OrderPlatform.BuildingBlocks.Money;
using OrderPlatform.BuildingBlocks.Users;
using OrderPlatform.Catalog.Products;

namespace OrderPlatform.Persistence.Tests.Seeding;

public sealed class CatalogSeedDataTests : IDisposable
{
    private readonly AppDbContext _context = new(
        new DbContextOptionsBuilder<AppDbContext>().UseSqlServer("Server=unused;Database=unused").Options);

    private IModel Model => _context.GetService<IDesignTimeModel>().Model;

    private IReadOnlyList<IDictionary<string, object?>> Rows<T>() =>
        Model.FindEntityType(typeof(T))!.GetSeedData().ToList();

    [Fact]
    public void TaxCategories_AreTheGreekVatRates()
    {
        var rates = Rows<TaxCategory>().Select(row => (decimal)row[nameof(TaxCategory.Rate)]!).Order();

        Assert.Equal([0m, 0.06m, 0.13m, 0.24m], rates);
    }

    [Fact]
    public void Products_HaveUniqueUppercaseSkus()
    {
        var skus = Rows<Product>().Select(row => (string)row[nameof(Product.Sku)]!).ToList();

        Assert.Equal(9, skus.Count);
        Assert.Equal(skus.Count, skus.Distinct().Count());
        Assert.All(skus, sku => Assert.Equal(sku.ToUpperInvariant(), sku));
    }

    [Fact]
    public void Products_UseOnlySeededTaxCategories()
    {
        var categories = Rows<TaxCategory>().Select(row => (Guid)row[nameof(TaxCategory.Id)]!).ToHashSet();

        Assert.All(Rows<Product>(), row => Assert.Contains((Guid)row[nameof(Product.TaxCategoryId)]!, categories));
    }

    [Fact]
    public void Products_IncludePhysicalAndDigitalOnes()
    {
        var products = Rows<Product>();

        Assert.Contains(products, row => (bool)row[nameof(Product.IsPhysical)]!);
        Assert.Contains(products, row => !(bool)row[nameof(Product.IsPhysical)]!);
    }

    [Fact]
    public void EveryProduct_HasExactlyOneOpenPositiveEuroPrice()
    {
        var prices = Rows<ProductPrice>();

        Assert.All(Rows<Product>(), product =>
        {
            var id = (Guid)product[nameof(Product.Id)]!;
            var open = Assert.Single(prices, price => (Guid)price[nameof(ProductPrice.ProductId)]! == id && price[nameof(ProductPrice.ValidTo)] is null);
            Assert.True((decimal)open[nameof(ProductPrice.Amount)]! > 0);
            Assert.Equal(Currency.EUR, (Currency)open[nameof(ProductPrice.Currency)]!);
        });
    }

    [Fact]
    public void OneProduct_HasAPriceHistory_ThatHandsOverWithoutAGap()
    {
        var history = Rows<ProductPrice>()
            .GroupBy(price => (Guid)price[nameof(ProductPrice.ProductId)]!)
            .Single(group => group.Count() > 1)
            .OrderBy(price => (DateTimeOffset)price[nameof(ProductPrice.ValidFrom)]!)
            .ToList();

        Assert.Equal(history[1][nameof(ProductPrice.ValidFrom)], history[0][nameof(ProductPrice.ValidTo)]);
    }

    [Fact]
    public void EverySeededRow_IsCreatedByTheSystemUser()
    {
        var rows = Rows<TaxCategory>().Concat(Rows<Product>()).Concat(Rows<ProductPrice>());

        Assert.All(rows, row => Assert.Equal(SystemUser.Id, row[nameof(Product.CreatedBy)]));
    }

    public void Dispose() => _context.Dispose();
}
