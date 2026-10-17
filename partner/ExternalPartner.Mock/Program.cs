using System.Collections.Concurrent;

// Mock of the external partner. It keeps what it receives in memory, keyed by loan application id.
var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

var received = new ConcurrentDictionary<Guid, ReceivedLoanApplication>();

app.MapPost("/applications", (LoanApplication application, ILogger<Program> logger) => Store(application, logger));

// Creates the application if it doesn't exist, so it's safe to send the same one twice.
app.MapPut("/applications/{id:guid}", (Guid id, LoanApplication application, ILogger<Program> logger) =>
    id == application.Id
        ? Store(application, logger)
        : Results.ValidationProblem(new Dictionary<string, string[]> { ["id"] = ["Must match the id in the body."] }));

app.MapGet("/applications", () => received.Values.OrderBy(r => r.ReceivedAt));

app.Run();

IResult Store(LoanApplication application, ILogger logger)
{
    var errors = Validate(application);
    if (errors.Count > 0)
        return Results.ValidationProblem(errors);

    received[application.Id] = new ReceivedLoanApplication(application, DateTimeOffset.UtcNow);
    logger.LogInformation("Received loan application {LoanApplicationId}.", application.Id);
    return Results.Ok();
}

static Dictionary<string, string[]> Validate(LoanApplication application)
{
    var errors = new Dictionary<string, string[]>();
    if (application.Id == Guid.Empty)
        errors["id"] = ["Is required."];
    foreach (var (field, value) in new[]
    {
        ("firstName", application.FirstName), ("lastName", application.LastName), ("address", application.Address),
        ("state", application.State), ("companyName", application.CompanyName),
    })
    {
        if (string.IsNullOrWhiteSpace(value))
            errors[field] = ["Is required."];
    }

    if (application.Ssn is not { Length: 9 } || !application.Ssn.All(char.IsAsciiDigit))
        errors["ssn"] = ["Must have 9 digits."];
    if (application.RequestedAmount <= 0)
        errors["requestedAmount"] = ["Must be greater than zero."];
    return errors;
}

internal sealed record LoanApplication(
    Guid Id,
    string FirstName,
    string LastName,
    string Address,
    string State,
    string CompanyName,
    string Ssn,
    decimal RequestedAmount);

internal sealed record ReceivedLoanApplication(LoanApplication LoanApplication, DateTimeOffset ReceivedAt);
