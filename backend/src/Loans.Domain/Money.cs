using System.Globalization;

namespace Loans.Domain;

/// <summary>An amount in US dollars: greater than zero, with at most 2 decimal places.</summary>
public sealed record Money
{
    // A sanity limit; it also fits numeric(18,2) storage.
    private const decimal Max = 9_999_999_999_999_999.99m;

    private Money(decimal amount) => Amount = amount;

    public decimal Amount { get; }

    public static Money Of(decimal amount, string field = nameof(Money))
    {
        if (amount <= 0)
            throw new ValidationException(field, "Must be greater than zero.");
        if (decimal.Round(amount, 2) != amount)
            throw new ValidationException(field, "Can have at most 2 decimal places.");
        if (amount > Max)
            throw new ValidationException(field, "Is too large.");

        return new Money(amount);
    }

    public override string ToString() => Amount.ToString("C", CultureInfo.GetCultureInfo("en-US"));
}
