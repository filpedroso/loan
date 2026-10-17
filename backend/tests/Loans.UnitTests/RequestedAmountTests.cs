using System.Globalization;
using Loans.Domain;
using Loans.Domain.Customers;

namespace Loans.UnitTests;

public class RequestedAmountTests
{
    [Theory]
    [InlineData("0.01")]
    [InlineData("1500")]
    [InlineData("250000.50")]
    public void Of_accepts_positive_amounts_with_up_to_two_decimals(string amount)
    {
        Assert.Equal(Parse(amount), RequestedAmount.Of(Parse(amount)).Value);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("10.001")]
    [InlineData("10000000000000000")]
    public void Of_rejects_zero_negative_too_precise_or_too_large_amounts(string amount)
    {
        var error = Assert.Throws<ValidationException>(() => RequestedAmount.Of(Parse(amount)));

        Assert.Equal("RequestedAmount", error.Field);
    }

    private static decimal Parse(string amount) => decimal.Parse(amount, CultureInfo.InvariantCulture);
}
