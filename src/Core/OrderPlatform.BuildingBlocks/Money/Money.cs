namespace OrderPlatform.BuildingBlocks.Money;

/// <summary>
/// An amount in a currency. All rounding of money happens here: to cents, half away from zero.
/// Amounts in different currencies are never combined.
/// </summary>
public readonly record struct Money(decimal Amount, Currency Currency)
{
    private const int Cents = 2;

    public static Money Zero(Currency currency) => new(0m, currency);

    public static Money operator +(Money left, Money right)
    {
        left.EnsureSameCurrency(right);
        return new Money(left.Amount + right.Amount, left.Currency);
    }

    public static Money operator *(Money money, int quantity) => new(money.Amount * quantity, money.Currency);

    /// <summary>The given fraction of this amount, rounded to cents. A rate of 0.24 gives 24%.</summary>
    public Money PercentOf(decimal rate) => new(RoundToCents(Amount * rate), Currency);

    public Money RoundedToCents() => new(RoundToCents(Amount), Currency);

    private static decimal RoundToCents(decimal amount) => Math.Round(amount, Cents, MidpointRounding.AwayFromZero);

    private void EnsureSameCurrency(Money other)
    {
        if (other.Currency != Currency)
        {
            throw new InvalidOperationException($"Cannot combine {Currency} with {other.Currency}.");
        }
    }
}
