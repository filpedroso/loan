using Loans.Domain;
using Loans.Domain.Customers;

namespace Loans.UnitTests;

public class SsnTests
{
    [Theory]
    [InlineData("987-65-4320")]
    [InlineData("987654320")]
    [InlineData(" 987-65-4320 ")]
    public void Parse_accepts_nine_digits_with_or_without_dashes(string input)
    {
        Assert.Equal("987654320", Ssn.Parse(input).Value);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("98765432")]
    [InlineData("9876543201")]
    [InlineData("987-65-432a")]
    public void Parse_rejects_anything_but_nine_digits(string? input)
    {
        var error = Assert.Throws<ValidationException>(() => Ssn.Parse(input));

        Assert.Equal("Ssn", error.Field);
    }

    [Fact]
    public void ToString_masks_all_but_the_last_four_digits()
    {
        var ssn = Ssn.Parse("987-65-4320");

        Assert.Equal("***-**-4320", ssn.ToString());
        Assert.Equal("SSN ***-**-4320", $"SSN {ssn}");
    }

    [Fact]
    public void Ssns_with_the_same_digits_are_equal()
    {
        Assert.Equal(Ssn.Parse("987-65-4320"), Ssn.Parse("987654320"));
    }
}
