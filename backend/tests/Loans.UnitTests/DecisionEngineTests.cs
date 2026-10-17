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
        Assert.Null(decision.Reason);
    }

    [Fact]
    public async Task Denies_with_the_first_failing_rule_and_skips_the_rest()
    {
        var skipped = new FixedRule(false, DenialReason.SsnBlocklisted);
        var engine = new DecisionEngine([
            new FixedRule(true, DenialReason.SsnBlocklisted),
            new FixedRule(false, DenialReason.StateNotServed),
            skipped,
        ]);

        var decision = await engine.DecideAsync(AnyCustomer, TestContext.Current.CancellationToken);

        Assert.False(decision.Approved);
        Assert.Equal(DenialReason.StateNotServed, decision.Reason);
        Assert.False(skipped.Evaluated);
    }

    private sealed class FixedRule(bool satisfied, DenialReason reason) : IDecisionRule
    {
        public bool Evaluated { get; private set; }

        public DenialReason Reason => reason;

        public ValueTask<bool> IsSatisfiedAsync(Customer customer, CancellationToken ct)
        {
            Evaluated = true;
            return ValueTask.FromResult(satisfied);
        }
    }
}
