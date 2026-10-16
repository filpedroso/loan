using Loans.Domain;
using Loans.Domain.Customers;
using Loans.Domain.Decisions;

namespace Loans.UnitTests;

public class DecisionEngineTests
{
    private static readonly Customer AnyCustomer =
        Customer.Register(Ssn.Parse("987-65-4320"), "Jane", "Doe", "1 Main St", "CA", "Acme");

    [Fact]
    public async Task Approves_when_every_rule_is_satisfied()
    {
        var engine = new DecisionEngine([new FixedRule(true, DenialReason.StateNotServed)]);

        var decision = await engine.DecideAsync(AnyCustomer, TestContext.Current.CancellationToken);

        Assert.True(decision.Approved);
        Assert.Empty(decision.Reasons);
    }

    [Fact]
    public async Task Denies_with_the_reason_of_every_failing_rule()
    {
        var engine = new DecisionEngine([
            new FixedRule(false, DenialReason.StateNotServed),
            new FixedRule(true, DenialReason.StateNotServed),
            new FixedRule(false, DenialReason.SsnBlocklisted),
        ]);

        var decision = await engine.DecideAsync(AnyCustomer, TestContext.Current.CancellationToken);

        Assert.False(decision.Approved);
        Assert.Equal([DenialReason.StateNotServed, DenialReason.SsnBlocklisted], decision.Reasons);
    }

    private sealed class FixedRule(bool satisfied, DenialReason reason) : IDecisionRule
    {
        public DenialReason Reason => reason;

        public ValueTask<bool> IsSatisfiedAsync(Customer customer, CancellationToken ct) => ValueTask.FromResult(satisfied);
    }
}
