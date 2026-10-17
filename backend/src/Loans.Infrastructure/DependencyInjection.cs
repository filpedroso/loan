using System.Diagnostics;
using Loans.Domain.Decisions;
using Loans.Infrastructure.ExternalPartner;
using Loans.Infrastructure.Outbox;
using Loans.Infrastructure.Persistence;
using Loans.Infrastructure.Security;
using Loans.UseCases;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Loans.Infrastructure;

public static class DependencyInjection
{
    /// <summary>Registers the adapters behind the ports and the outbox worker. Settings are read on first use.</summary>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        services.AddSingleton(sp => SsnProtector.FromConfiguration(sp.GetRequiredService<IConfiguration>()));
        services.AddDbContext<LoansDbContext>((sp, options) => options
            .UseNpgsql(sp.GetRequiredService<IConfiguration>().GetConnectionString("Loans"), npgsql => npgsql
                // Trace a command only as part of something else, like a request. Otherwise the outbox worker's
                // polling query adds a trace to the dashboard every second.
                .ConfigureDataSource(dataSource => dataSource.ConfigureTracing(tracing =>
                    tracing.ConfigureCommandFilter(_ => Activity.Current is not null))))
            .UseSnakeCaseNamingConvention());
        services.AddScoped<ICustomerRepository, CustomerRepository>();
        services.AddScoped<ISsnBlocklist, SsnBlocklist>();

        services.AddScoped<OutboxDispatcher>();
        services.AddHostedService<OutboxWorker>();
        services.AddHttpClient<ExternalPartnerClient>((sp, http) =>
        {
            http.BaseAddress = new Uri(sp.GetRequiredService<IConfiguration>()["ExternalPartner:BaseUrl"]
                ?? throw new InvalidOperationException("Missing configuration: ExternalPartner:BaseUrl."));
            http.Timeout = TimeSpan.FromSeconds(5);
        });

        return services;
    }
}
