using System.Security.Cryptography;
using Loans.Infrastructure.Crm;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;

namespace Loans.IntegrationTests;

/// <summary>The real API against a throwaway Postgres container, with a stub in place of the CRM.</summary>
public sealed class LoansApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer postgres = new PostgreSqlBuilder("postgres:17.11-alpine").Build();

    public StubCrmHandler Crm { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:Loans", postgres.GetConnectionString());
        builder.UseSetting("Ssn:HashKey", NewKey());
        builder.UseSetting("Ssn:EncryptionKey", NewKey());
        builder.UseSetting("Outbox:Enabled", "false"); // Tests run the dispatcher themselves.
        builder.UseSetting("Outbox:BatchSize", "2");
        builder.UseSetting("RateLimiting:PermitLimit", "100000");
        builder.ConfigureTestServices(services =>
            services.AddHttpClient<CrmClient>().ConfigurePrimaryHttpMessageHandler(() => Crm));
    }

    public async ValueTask InitializeAsync() => await postgres.StartAsync();

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        await postgres.DisposeAsync();
    }

    private static string NewKey() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
}
