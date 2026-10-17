using Loans.Domain.Customers;
using Loans.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Loans.Infrastructure.Persistence;

public static class DatabaseInitializer
{
    /// <summary>
    /// Applies migrations (EF Core takes a database lock, so replicas starting together don't conflict),
    /// then adds the SSNs in <c>Blocklist:Seed</c> that the blocklist doesn't have yet.
    /// </summary>
    public static async Task InitializeDatabaseAsync(this IServiceProvider services, CancellationToken ct = default)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<LoansDbContext>();
        // EF's own script, so the history table exists before MigrateAsync looks for it. Otherwise a fresh database
        // logs a harmless "Failed executing DbCommand" for that first lookup.
        await db.Database.ExecuteSqlRawAsync(db.GetService<IHistoryRepository>().GetCreateIfNotExistsScript(), ct);
        await db.Database.MigrateAsync(ct);

        // Every SSN is parsed before anything is written, so a typo stops startup with nothing half-seeded.
        var ssnProtector = scope.ServiceProvider.GetRequiredService<SsnProtector>();
        var hashes = (scope.ServiceProvider.GetRequiredService<IConfiguration>()
                .GetSection("Blocklist:Seed").Get<string[]>() ?? [])
            .Select(ssn => ssnProtector.Hash(Ssn.Parse(ssn)))
            .ToList();

        // Runs on every start, so SSNs added to the seed later are inserted too. ON CONFLICT skips the ones
        // already there, including any another replica inserts at the same time.
        foreach (var hash in hashes)
            await db.Database.ExecuteSqlAsync($"INSERT INTO blocklist (ssn_hash) VALUES ({hash}) ON CONFLICT DO NOTHING", ct);
    }
}
