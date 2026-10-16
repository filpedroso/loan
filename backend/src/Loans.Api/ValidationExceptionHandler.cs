using System.Text.Json;
using Loans.Domain;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Loans.Api;

/// <summary>Turns a domain ValidationException into a 400 ValidationProblemDetails that names the field.</summary>
internal sealed class ValidationExceptionHandler(IProblemDetailsService problemDetails) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext http, Exception exception, CancellationToken ct)
    {
        if (exception is not ValidationException validation)
            return false;

        http.Response.StatusCode = StatusCodes.Status400BadRequest;
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = http,
            Exception = exception,
            ProblemDetails = new ValidationProblemDetails(new Dictionary<string, string[]>
            {
                [JsonNamingPolicy.CamelCase.ConvertName(validation.Field)] = [validation.Message],
            }),
        });
    }
}
