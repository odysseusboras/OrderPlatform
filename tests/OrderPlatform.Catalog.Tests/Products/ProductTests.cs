using OrderPlatform.BuildingBlocks.Money;
using OrderPlatform.Catalog.Products;

namespace OrderPlatform.Catalog.Tests.Products;

public sealed class ProductTests
{
    private static readonly Guid TaxCategoryId = Guid.NewGuid();
    private static readonly DateTimeOffset January = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset June = new(2026, 6, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Create_NormalisesSkuAndName_AndStartsActive()
    {
        var product = Product.Create(" kb-01 ", " Keyboard ", isPhysical: true, TaxCategoryId);

        Assert.Equal("KB-01", product.Sku);
        Assert.Equal("Keyboard", product.Name);
        Assert.True(product.IsPhysical);
        Assert.True(product.IsActive);
        Assert.Equal(TaxCategoryId, product.TaxCategoryId);
        Assert.Empty(product.Prices);
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void Create_WithBlankSku_Throws(string sku)
    {
        Assert.ThrowsAny<ArgumentException>(() => Product.Create(sku, "Keyboard", true, TaxCategoryId));
    }

    [Fact]
    public void Create_WithBlankName_Throws()
    {
        Assert.ThrowsAny<ArgumentException>(() => Product.Create("KB-01", " ", true, TaxCategoryId));
    }

    [Fact]
    public void Create_WithoutTaxCategory_Throws()
    {
        Assert.Throws<ArgumentException>(() => Product.Create("KB-01", "Keyboard", true, Guid.Empty));
    }

    [Fact]
    public void SetPrice_First_IsValidFromItsStartWithoutEnd()
    {
        var product = NewProduct();

        product.SetPrice(new Money(49.90m, Currency.EUR), January);

        var price = Assert.Single(product.Prices);
        Assert.Equal(new Money(49.90m, Currency.EUR), price.Price);
        Assert.Equal(January, price.ValidFrom);
        Assert.Null(price.ValidTo);
        Assert.Equal(product.Id, price.ProductId);
    }

    [Fact]
    public void SetPrice_Second_ClosesTheFirstAtTheNewStart()
    {
        var product = NewProduct();
        product.SetPrice(new Money(49.90m, Currency.EUR), January);

        product.SetPrice(new Money(44.90m, Currency.EUR), June);

        Assert.Equal(June, product.Prices[0].ValidTo);
        Assert.Null(product.Prices[1].ValidTo);
    }

    [Fact]
    public void SetPrice_NotAfterTheLatestPrice_Throws()
    {
        var product = NewProduct();
        product.SetPrice(new Money(49.90m, Currency.EUR), June);

        Assert.Throws<InvalidOperationException>(() => product.SetPrice(new Money(44.90m, Currency.EUR), June));
    }

    [Fact]
    public void SetPrice_Negative_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => NewProduct().SetPrice(new Money(-1m, Currency.EUR), January));
    }

    [Fact]
    public void SetPrice_WithAnUnknownCurrency_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => NewProduct().SetPrice(new Money(1m, (Currency)999), January));
    }

    [Fact]
    public void PriceAt_ReturnsThePriceValidAtThatMoment()
    {
        var product = NewProduct();
        product.SetPrice(new Money(49.90m, Currency.EUR), January);
        product.SetPrice(new Money(44.90m, Currency.EUR), June);

        Assert.Null(product.PriceAt(January.AddDays(-1)));
        Assert.Equal(49.90m, product.PriceAt(January)?.Amount);
        Assert.Equal(49.90m, product.PriceAt(June.AddTicks(-1))?.Amount);
        Assert.Equal(44.90m, product.PriceAt(June)?.Amount);
    }

    [Fact]
    public void Deactivate_MakesTheProductInactive()
    {
        var product = NewProduct();

        product.Deactivate();

        Assert.False(product.IsActive);
    }

    private static Product NewProduct() => Product.Create("KB-01", "Keyboard", true, TaxCategoryId);
}
