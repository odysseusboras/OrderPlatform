using OrderPlatform.BuildingBlocks.Money;
using OrderPlatform.Catalog.Products;

namespace OrderPlatform.Catalog.Tests.Products;

/// <summary>The in-memory rule and the query rule for price validity must always agree.</summary>
public sealed class ProductPriceTests
{
    private static readonly DateTimeOffset January = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset June = new(2026, 6, 1, 0, 0, 0, TimeSpan.Zero);

    public static TheoryData<DateTimeOffset, bool> Moments => new()
    {
        { January.AddTicks(-1), false },
        { January, true },
        { June.AddTicks(-1), true },
        { June, false },
    };

    [Theory]
    [MemberData(nameof(Moments))]
    public void IsValidAt_AndValidAt_AgreeAtTheBoundaries(DateTimeOffset at, bool expected)
    {
        var product = Product.Create("KB-01", "Keyboard", true, Guid.NewGuid());
        product.SetPrice(new Money(89.90m, Currency.EUR), January);
        product.SetPrice(new Money(79.90m, Currency.EUR), June);
        var first = product.Prices[0];

        Assert.Equal(expected, first.IsValidAt(at));
        Assert.Equal(expected, ProductPrice.ValidAt(at).Compile()(first));
    }
}
