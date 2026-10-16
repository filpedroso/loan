using Loans.Domain.Customers;

namespace Loans.Domain.Decisions;

/// <summary>
/// One rule of the decision engine. Synchronous rules return a completed ValueTask at no cost;
/// only rules that do I/O await. Adding a rule means a new class plus one DI line; existing rules don't change.
/// </summary>
public interface IDecisionRule
{
    DenialReason Reason { get; }

    ValueTask<bool> IsSatisfiedAsync(Customer customer, CancellationToken ct);
}
