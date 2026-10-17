using System.Net;

namespace Loans.IntegrationTests;

/// <summary>Stands in for the external partner: records every call and answers with <see cref="Status"/>.</summary>
public sealed class StubExternalPartnerHandler : HttpMessageHandler
{
    private readonly List<ExternalPartnerCall> calls = [];

    /// <summary>The status to answer with. Null simulates a network error.</summary>
    public HttpStatusCode? Status { get; private set; } = HttpStatusCode.OK;

    public IReadOnlyList<ExternalPartnerCall> Calls
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
        lock (calls) calls.Add(new ExternalPartnerCall(request.Method, request.RequestUri!.AbsolutePath, body));

        return Status is { } status
            ? new HttpResponseMessage(status)
            : throw new HttpRequestException("Simulated network error.");
    }
}

public sealed record ExternalPartnerCall(HttpMethod Method, string Path, string Body);
