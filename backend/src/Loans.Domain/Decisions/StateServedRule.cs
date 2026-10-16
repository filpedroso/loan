using Loans.Domain.Customers;

namespace Loans.Domain.Decisions;

/// <summary>Denies customers who live in a state we don't serve. The list comes from configuration.</summary>
public sealed class StateServedRule(IReadOnlySet<string> blockedStates) : IDecisionRule
{
    public DenialReason Reason => DenialReason.StateNotServed;

    public ValueTask<bool> IsSatisfiedAsync(Customer customer, CancellationToken ct) =>
        ValueTask.FromResult(!blockedStates.Contains(customer.State));
}
