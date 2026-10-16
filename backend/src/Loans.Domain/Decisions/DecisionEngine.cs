using Loans.Domain.Customers;

namespace Loans.Domain.Decisions;

public sealed class DecisionEngine(IEnumerable<IDecisionRule> rules)
{
    /// <summary>Runs every rule, without stopping at the first failure, so a denial lists all its reasons.</summary>
    public async Task<Decision> DecideAsync(Customer customer, CancellationToken ct)
    {
        var reasons = new List<DenialReason>();
        foreach (var rule in rules)
        {
            if (!await rule.IsSatisfiedAsync(customer, ct))
                reasons.Add(rule.Reason);
        }

        return new Decision(reasons);
    }
}
