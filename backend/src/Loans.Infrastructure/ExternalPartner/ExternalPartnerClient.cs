using System.Net;
using System.Net.Http.Json;
using Loans.Domain.Customers;

namespace Loans.Infrastructure.ExternalPartner;

/// <summary>Adapter for the external partner. Retries are the outbox's job, not this client's.</summary>
public sealed class ExternalPartnerClient(HttpClient http)
{
    /// <summary>PUT creates or overwrites, so sending the same application twice is harmless.</summary>
    public async Task<HttpStatusCode> PutLoanApplicationAsync(Customer customer, CancellationToken ct)
    {
        var loanApplication = customer.LoanApplication
            ?? throw new InvalidOperationException("Only customers with a loan application are sent to the external partner.");
        var payload = new LoanApplicationPayload(
            loanApplication.Id,
            customer.FirstName,
            customer.LastName,
            customer.Address,
            customer.State,
            customer.CompanyName,
            customer.Ssn.Value,
            loanApplication.RequestedAmount.Value);

        using var response = await http.PutAsJsonAsync($"applications/{loanApplication.Id}", payload, ct);
        return response.StatusCode;
    }
}

internal sealed record LoanApplicationPayload(
    Guid Id,
    string FirstName,
    string LastName,
    string Address,
    string State,
    string CompanyName,
    string Ssn,
    decimal RequestedAmount);
