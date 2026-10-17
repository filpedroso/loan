using Loans.Domain.Customers;
using Loans.Domain.Decisions;

namespace Loans.UnitTests;

public class SsnNotBlocklistedRuleTests
{
    private readonly SsnNotBlocklistedRule rule = new(new InMemoryBlocklist(Ssn.Parse("987-65-4329")));

    [Theory]
    [InlineData("987-65-4329", false)]
    [InlineData("987-65-4320", true)]
    public async Task Is_satisfied_unless_the_ssn_is_blocklisted(string ssn, bool expected)
    {
        var customer = Customer.Register(Ssn.Parse(ssn), "Jane", "Doe", "1 Main St", "CA", "Acme");

        Assert.Equal(expected, await rule.IsSatisfiedAsync(customer, TestContext.Current.CancellationToken));
    }

    private sealed class InMemoryBlocklist(params Ssn[] blocked) : ISsnBlocklist
    {
        public Task<bool> ContainsAsync(Ssn ssn, CancellationToken ct) => Task.FromResult(blocked.Contains(ssn));
    }
}
