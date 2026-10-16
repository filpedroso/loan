using System.Net;

namespace Loans.IntegrationTests;

/// <summary>Stands in for the CRM: records every call and answers with <see cref="Status"/>.</summary>
public sealed class StubCrmHandler : HttpMessageHandler
{
    private readonly List<CrmCall> calls = [];

    /// <summary>The status to answer with. Null simulates a network error.</summary>
    public HttpStatusCode? Status { get; private set; } = HttpStatusCode.OK;

    public IReadOnlyList<CrmCall> Calls
    {
        get { lock (calls) return [.. calls]; }
    }

    public void Reset(HttpStatusCode? status = HttpStatusCode.OK)
    {
        lock (calls) calls.Clear();
        Status = status;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var body = request.Content is null ? "" : await request.Content.ReadAsStringAsync(ct);
        lock (calls) calls.Add(new CrmCall(request.Method, request.RequestUri!.AbsolutePath, body));

        return Status is { } status
            ? new HttpResponseMessage(status)
            : throw new HttpRequestException("Simulated network error.");
    }
}

public sealed record CrmCall(HttpMethod Method, string Path, string Body);
