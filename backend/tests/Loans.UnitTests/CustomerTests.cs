using Loans.Domain;
using Loans.Domain.Customers;

namespace Loans.UnitTests;

public class CustomerTests
{
    private static readonly Ssn AnySsn = Ssn.Parse("987-65-4320");

    [Fact]
    public void Register_creates_a_customer_with_trimmed_details_and_no_application()
    {
        var customer = Customer.Register(AnySsn, " Jane ", "Doe", "1 Main St, Springfield", "ca", "Acme Corp");

        Assert.NotEqual(Guid.Empty, customer.Id);
        Assert.Equal(AnySsn, customer.Ssn);
        Assert.Equal("Jane", customer.FirstName);
        Assert.Equal("CA", customer.State);
        Assert.Null(customer.Application);
    }

    [Theory]
    [InlineData("", "Doe", "1 Main St", "CA", "Acme", "FirstName")]
    [InlineData("Jane", " ", "1 Main St", "CA", "Acme", "LastName")]
    [InlineData("Jane", "Doe", "", "CA", "Acme", "Address")]
    [InlineData("Jane", "Doe", "1 Main St", "XX", "Acme", "State")]
    [InlineData("Jane", "Doe", "1 Main St", "CA", "", "CompanyName")]
    public void Register_rejects_invalid_details(
        string firstName, string lastName, string address, string state, string companyName, string field)
    {
        var error = Assert.Throws<ValidationException>(
            () => Customer.Register(AnySsn, firstName, lastName, address, state, companyName));

        Assert.Equal(field, error.Field);
    }

    [Fact]
    public void Register_rejects_details_longer_than_the_limit()
    {
        var error = Assert.Throws<ValidationException>(
            () => Customer.Register(AnySsn, new string('a', 101), "Doe", "1 Main St", "CA", "Acme"));

        Assert.Equal("FirstName", error.Field);
    }

    [Fact]
    public void UpdateDetails_overwrites_the_details_and_keeps_the_identity()
    {
        var customer = Register();
        var id = customer.Id;

        customer.UpdateDetails("John", "Smith", "2 Oak Ave, Austin", "TX", "Globex");

        Assert.Equal(id, customer.Id);
        Assert.Equal(AnySsn, customer.Ssn);
        Assert.Equal(
            ("John", "Smith", "2 Oak Ave, Austin", "TX", "Globex"),
            (customer.FirstName, customer.LastName, customer.Address, customer.State, customer.CompanyName));
    }

    [Fact]
    public void UpdateDetails_with_an_invalid_field_changes_nothing()
    {
        var customer = Register();

        Assert.Throws<ValidationException>(() => customer.UpdateDetails("John", "Smith", "2 Oak Ave", "XX", "Globex"));

        Assert.Equal("Jane", customer.FirstName);
    }

    [Fact]
    public void Apply_creates_an_application_owned_by_the_customer()
    {
        var customer = Register();

        var application = customer.Apply(Money.Of(1500m));

        Assert.Same(application, customer.Application);
        Assert.Equal(customer.Id, application.CustomerId);
        Assert.Equal(Money.Of(1500m), application.RequestedAmount);
    }

    [Fact]
    public void Apply_again_overwrites_the_amount_and_keeps_the_application_id()
    {
        var customer = Register();
        var first = customer.Apply(Money.Of(1500m));

        var second = customer.Apply(Money.Of(2500m));

        Assert.Equal(first.Id, second.Id);
        Assert.Equal(Money.Of(2500m), second.RequestedAmount);
    }

    private static Customer Register() =>
        Customer.Register(AnySsn, "Jane", "Doe", "1 Main St, Springfield", "CA", "Acme Corp");
}
