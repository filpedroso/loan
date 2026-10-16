using System.Globalization;
using Loans.Domain;

namespace Loans.UnitTests;

public class MoneyTests
{
    [Theory]
    [InlineData("0.01")]
    [InlineData("1500")]
    [InlineData("250000.50")]
    public void Of_accepts_positive_amounts_with_up_to_two_decimals(string amount)
    {
        Assert.Equal(Parse(amount), Money.Of(Parse(amount)).Amount);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("10.001")]
    [InlineData("10000000000000000")]
    public void Of_rejects_zero_negative_too_precise_or_too_large_amounts(string amount)
    {
        var error = Assert.Throws<ValidationException>(() => Money.Of(Parse(amount), "RequestedAmount"));

        Assert.Equal("RequestedAmount", error.Field);
    }

    private static decimal Parse(string amount) => decimal.Parse(amount, CultureInfo.InvariantCulture);
}
