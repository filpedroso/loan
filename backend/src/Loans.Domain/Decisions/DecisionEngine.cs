using Loans.Domain.Customers;

namespace Loans.Domain.Decisions;

public sealed class DecisionEngine(IEnumerable<IDecisionRule> rules)
{
    /// <summary>
    /// Runs the rules in order and stops at the first one that fails, so a denial has one reason and later rules,
    /// like the blocklist lookup, don't run once the outcome is known.
    /// </summary>
    public async Task<Decision> DecideAsync(Customer customer, CancellationToken ct)
    {
        foreach (var rule in rules)
        {
            if (!await rule.IsSatisfiedAsync(customer, ct))
                return new Decision(rule.Reason);
        }

        return new Decision(null);
    }
}
