using Loans.Domain;
using Loans.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Loans.Infrastructure.Persistence;

public static class DatabaseInitializer
{
    /// <summary>
    /// Applies migrations (EF Core takes a database lock, so replicas starting together don't conflict),
    /// then seeds the blocklist from <c>Blocklist:Seed</c> if it's empty.
    /// </summary>
    public static async Task InitializeDatabaseAsync(this IServiceProvider services, CancellationToken ct = default)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<LoansDbContext>();
        await db.Database.MigrateAsync(ct);

        if (await db.Blocklist.AnyAsync(ct))
            return;

        var ssnProtector = scope.ServiceProvider.GetRequiredService<SsnProtector>();
        var seed = scope.ServiceProvider.GetRequiredService<IConfiguration>()
            .GetSection("Blocklist:Seed").Get<string[]>() ?? [];
        foreach (var ssn in seed)
        {
            // ON CONFLICT: another replica may be seeding at the same time.
            var hash = ssnProtector.Hash(Ssn.Parse(ssn));
            await db.Database.ExecuteSqlAsync($"INSERT INTO blocklist (ssn_hash) VALUES ({hash}) ON CONFLICT DO NOTHING", ct);
        }
    }
}
