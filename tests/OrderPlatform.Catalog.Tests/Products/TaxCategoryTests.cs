using OrderPlatform.Catalog.Products;

namespace OrderPlatform.Catalog.Tests.Products;

public sealed class TaxCategoryTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(0.24)]
    [InlineData(1)]
    public void Create_WithRateBetweenZeroAndOne_Succeeds(decimal rate)
    {
        var category = TaxCategory.Create(" Standard ", rate);

        Assert.Equal("Standard", category.Name);
        Assert.Equal(rate, category.Rate);
    }

    [Theory]
    [InlineData(-0.01)]
    [InlineData(1.01)]
    public void Create_WithRateOutsideZeroAndOne_Throws(decimal rate)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => TaxCategory.Create("Standard", rate));
    }

    [Fact]
    public void Create_WithBlankName_Throws()
    {
        Assert.ThrowsAny<ArgumentException>(() => TaxCategory.Create(" ", 0.24m));
    }
}
