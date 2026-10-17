using System.Globalization;

namespace Loans.Domain.Customers;

/// <summary>What a loan application asks for, in US dollars: greater than zero, with at most 2 decimal places.</summary>
public sealed record RequestedAmount
{
    // A sanity limit; it also fits numeric(18,2) storage.
    private const decimal Max = 9_999_999_999_999_999.99m;

    private RequestedAmount(decimal value) => Value = value;

    public decimal Value { get; }

    public static RequestedAmount Of(decimal value)
    {
        if (value <= 0)
            throw new ValidationException(nameof(RequestedAmount), "Must be greater than zero.");
        if (decimal.Round(value, 2) != value)
            throw new ValidationException(nameof(RequestedAmount), "Can have at most 2 decimal places.");
        if (value > Max)
            throw new ValidationException(nameof(RequestedAmount), "Is too large.");

        return new RequestedAmount(value);
    }

    public override string ToString() => Value.ToString("C", CultureInfo.GetCultureInfo("en-US"));
}
