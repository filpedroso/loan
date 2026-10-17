using Loans.Domain.Customers;

namespace Loans.UseCases;

/// <summary>Port for storing customers. Implemented by Infrastructure.</summary>
public interface ICustomerRepository
{
    /// <summary>
    /// Locks the SSN until the request ends, so submissions for the same SSN take turns, then loads the customer
    /// with their loan application. Call it before <see cref="SaveAsync"/>.
    /// </summary>
    Task<Customer?> LockAndFindBySsnAsync(Ssn ssn, CancellationToken ct);

    /// <summary>
    /// Saves the customer and their loan application in one transaction. Every saved loan application is synced
    /// to the external partner.
    /// </summary>
    Task SaveAsync(Customer customer, CancellationToken ct);
}
