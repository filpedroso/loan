using Loans.Domain.Customers;

namespace Loans.UseCases;

/// <summary>Port for storing customers. Implemented by Infrastructure.</summary>
public interface ICustomerRepository
{
    /// <summary>
    /// Loads the customer with their loan application, and reserves the SSN until the request ends,
    /// so submissions for the same SSN take turns.
    /// </summary>
    Task<Customer?> FindBySsnAsync(Ssn ssn, CancellationToken ct);

    /// <summary>Saves the customer, their loan application and the CRM sync message, in one transaction.</summary>
    Task SaveApprovedAsync(Customer customer, CancellationToken ct);
}
