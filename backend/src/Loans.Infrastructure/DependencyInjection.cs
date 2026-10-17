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
            .UseNpgsql(sp.GetRequiredService<IConfiguration>().GetConnectionString("Loans"))
            .UseSnakeCaseNamingConvention());
        services.AddScoped<ICustomerRepository, CustomerRepository>();
        services.AddScoped<ISsnBlocklist, SsnBlocklist>();

        services.AddOptions<OutboxOptions>().BindConfiguration("Outbox");
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
