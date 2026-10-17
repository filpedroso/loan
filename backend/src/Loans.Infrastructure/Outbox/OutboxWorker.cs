using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Loans.Infrastructure.Outbox;

/// <summary>Runs a dispatcher tick on start and then every <see cref="Interval"/>.</summary>
public sealed class OutboxWorker(IServiceScopeFactory scopes, IConfiguration config, ILogger<OutboxWorker> logger)
    : BackgroundService
{
    /// <summary>Time between ticks, which is also the time between retries.</summary>
    public static readonly TimeSpan Interval = TimeSpan.FromSeconds(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // False in tests, and on every API instance but one when several run.
        if (!config.GetValue("Outbox:Enabled", true))
            return;

        using var timer = new PeriodicTimer(Interval);
        do
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<OutboxDispatcher>().DispatchAsync(stoppingToken);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                logger.LogError(ex, "Outbox tick failed; trying again on the next tick.");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
