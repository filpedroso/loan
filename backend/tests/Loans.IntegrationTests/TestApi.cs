using System.Net.Http.Json;
using Loans.Domain.Customers;
using Loans.Infrastructure.Outbox;
using Loans.Infrastructure.Persistence;
using Loans.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Loans.IntegrationTests;

public sealed record SubmitResponse(bool Approved, Guid? ApplicationId, string? Reason);

/// <summary>Helpers shared by the integration tests.</summary>
public static class TestApi
{
    private const string Url = "/api/v1/applications";

    /// <summary>A random SSN starting with 666, which the SSA never issues.</summary>
    public static string NewSsn() => $"666{Random.Shared.Next(0, 1_000_000):D6}";

    public static object Form(string ssn, string state = "CA", decimal amount = 1500m, string firstName = "Jane") => new
    {
        firstName,
        lastName = "Doe",
        address = "1 Main St, Springfield",
        state,
        companyName = "Acme Corp",
        ssn,
        requestedAmount = amount,
    };

    public static Task<HttpResponseMessage> PostAsync(this HttpClient client, object form) =>
        client.PostAsJsonAsync(Url, form, TestContext.Current.CancellationToken);

    public static async Task<SubmitResponse> SubmitAsync(this HttpClient client, object form)
    {
        var response = await client.PostAsync(form);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<SubmitResponse>(TestContext.Current.CancellationToken))!;
    }

    public static async Task<Customer?> FindCustomerAsync(this LoansApiFactory factory, string ssn)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<LoansDbContext>();
        var hash = scope.ServiceProvider.GetRequiredService<SsnProtector>().Hash(Ssn.Parse(ssn));

        return await db.Customers.AsNoTracking()
            .Include(c => c.LoanApplication)
            .SingleOrDefaultAsync(c => EF.Property<byte[]>(c, "SsnHash") == hash, TestContext.Current.CancellationToken);
    }

    public static async Task<List<OutboxMessage>> OutboxAsync(this LoansApiFactory factory, Guid loanApplicationId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<LoansDbContext>();

        return await db.Outbox.AsNoTracking()
            .Where(m => m.LoanApplicationId == loanApplicationId)
            .OrderBy(m => m.Id)
            .ToListAsync(TestContext.Current.CancellationToken);
    }

    public static async Task ExecuteSqlAsync(this LoansApiFactory factory, string sql)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<LoansDbContext>();
        await db.Database.ExecuteSqlRawAsync(sql, TestContext.Current.CancellationToken);
    }

    public static async Task DispatchOutboxAsync(this LoansApiFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<OutboxDispatcher>().DispatchAsync(TestContext.Current.CancellationToken);
    }
}
