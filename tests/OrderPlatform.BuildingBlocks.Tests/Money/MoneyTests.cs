using OrderPlatform.BuildingBlocks.Money;

namespace OrderPlatform.BuildingBlocks.Tests.Money;

public sealed class MoneyTests
{
    [Fact]
    public void Zero_HasNoAmount_InTheGivenCurrency()
    {
        Assert.Equal(new BuildingBlocks.Money.Money(0m, Currency.GBP), BuildingBlocks.Money.Money.Zero(Currency.GBP));
    }

    [Fact]
    public void Add_SameCurrency_SumsTheAmounts()
    {
        Assert.Equal(Eur(15.50m), Eur(10.25m) + Eur(5.25m));
    }

    [Fact]
    public void Add_DifferentCurrencies_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => Eur(1m) + new BuildingBlocks.Money.Money(1m, Currency.USD));
    }

    [Fact]
    public void Multiply_ByQuantity_ScalesTheAmount()
    {
        Assert.Equal(Eur(239.70m), Eur(79.90m) * 3);
    }

    [Theory]
    [InlineData(79.90, 0.24, 19.18)]
    [InlineData(18.50, 0.13, 2.41)]
    [InlineData(0.625, 0.20, 0.13)]
    [InlineData(10.00, 0, 0)]
    public void PercentOf_RoundsToCents_HalfAwayFromZero(decimal amount, decimal rate, decimal expected)
    {
        Assert.Equal(Eur(expected), Eur(amount).PercentOf(rate));
    }

    [Fact]
    public void RoundedToCents_RoundsHalfAwayFromZero()
    {
        Assert.Equal(Eur(2.13m), Eur(2.125m).RoundedToCents());
    }

    private static BuildingBlocks.Money.Money Eur(decimal amount) => new(amount, Currency.EUR);
}
