using Loans.Domain.Customers;
using Loans.Domain.Decisions;

namespace Loans.UnitTests;

public class StateServedRuleTests
{
    private readonly StateServedRule rule = new(new HashSet<string>(["NY"], StringComparer.OrdinalIgnoreCase));

    [Theory]
    [InlineData("NY", false)]
    [InlineData("ny", false)]
    [InlineData("CA", true)]
    public async Task Is_satisfied_unless_the_customer_lives_in_a_blocked_state(string state, bool expected)
    {
        var customer = Customer.Register(Ssn.Parse("987-65-4320"), "Jane", "Doe", "1 Main St", state, "Acme");

        Assert.Equal(expected, await rule.IsSatisfiedAsync(customer, TestContext.Current.CancellationToken));
    }
}
