using Loans.Domain.Customers;

namespace Loans.Domain.Decisions;

public sealed class SsnNotBlocklistedRule(ISsnBlocklist blocklist) : IDecisionRule
{
    public DenialReason Reason => DenialReason.SsnBlocklisted;

    public async ValueTask<bool> IsSatisfiedAsync(Customer customer, CancellationToken ct) =>
        !await blocklist.ContainsAsync(customer.Ssn, ct);
}
