using System.Collections.Frozen;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Loans.Api;
using Loans.Domain.Decisions;
using Loans.Infrastructure;
using Loans.Infrastructure.Persistence;
using Loans.UseCases;
using Npgsql;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

var builder = WebApplication.CreateBuilder(args);

// Domain and Application: the decision rules (in order), the engine and the use case.
builder.Services.AddSingleton<IDecisionRule>(sp => new StateServedRule(
    (sp.GetRequiredService<IConfiguration>().GetSection("Decision:BlockedStates").Get<string[]>() ?? [])
    .ToFrozenSet(StringComparer.OrdinalIgnoreCase)));
builder.Services.AddScoped<IDecisionRule, SsnNotBlocklistedRule>();
builder.Services.AddScoped<DecisionEngine>();
builder.Services.AddScoped<SubmitLoanApplication>();

builder.Services.AddInfrastructure();

builder.Services.AddControllers()
    .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ValidationExceptionHandler>();
builder.Services.AddCors(options => options.AddDefaultPolicy(policy => policy
    .WithOrigins(builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [])
    .WithMethods("POST")
    .WithHeaders("Content-Type")));
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy(ApplicationsController.RateLimitPolicy, http => RateLimitPartition.GetFixedWindowLimiter(
        http.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = http.RequestServices.GetRequiredService<IConfiguration>().GetValue("RateLimiting:PermitLimit", 20),
            Window = TimeSpan.FromMinutes(1),
        }));
});
builder.Services.AddHealthChecks().AddDbContextCheck<LoansDbContext>();

// Logs, traces and metrics go to the OTLP endpoint (the Aspire Dashboard) when one is configured.
if (!string.IsNullOrEmpty(builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]))
{
    builder.Services.AddOpenTelemetry()
        .ConfigureResource(resource => resource.AddService("loans-api"))
        // Npgsql: a span per database command, so a slow trace shows time spent in PostgreSQL, and pool metrics.
        .WithTracing(tracing => tracing.AddAspNetCoreInstrumentation().AddHttpClientInstrumentation().AddNpgsql())
        .WithMetrics(metrics => metrics.AddAspNetCoreInstrumentation().AddHttpClientInstrumentation().AddNpgsqlInstrumentation())
        // Without the formatted message, the dashboard shows the raw template, e.g. "{elapsed}ms".
        .WithLogging(configureBuilder: null, configureOptions: logging => logging.IncludeFormattedMessage = true)
        .UseOtlpExporter();
}

var app = builder.Build();

app.UseExceptionHandler();
app.UseCors();
app.UseRateLimiter();
app.MapControllers();
app.MapHealthChecks("/health");

await app.Services.InitializeDatabaseAsync();
await app.RunAsync();
