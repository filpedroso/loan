using System.Net;
using System.Net.Http.Json;
using Loans.Domain.Customers;

namespace Loans.Infrastructure.Crm;

/// <summary>Adapter for the external CRM. Retries are the outbox's job, not this client's.</summary>
public sealed class CrmClient(HttpClient http)
{
    /// <summary>PUT creates or overwrites, so sending the same application twice is harmless.</summary>
    public async Task<HttpStatusCode> PutApplicationAsync(Customer customer, CancellationToken ct)
    {
        var application = customer.Application
            ?? throw new InvalidOperationException("Only customers with an application are sent to the CRM.");
        var payload = new CrmApplication(
            application.Id,
            customer.FirstName,
            customer.LastName,
            customer.Address,
            customer.State,
            customer.CompanyName,
            customer.Ssn.Value,
            application.RequestedAmount.Amount);

        using var response = await http.PutAsJsonAsync($"applications/{application.Id}", payload, ct);
        return response.StatusCode;
    }
}

public sealed record CrmApplication(
    Guid Id,
    string FirstName,
    string LastName,
    string Address,
    string State,
    string CompanyName,
    string Ssn,
    decimal RequestedAmount);
