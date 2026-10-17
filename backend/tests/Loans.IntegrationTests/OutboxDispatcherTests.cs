using System.Net;
using System.Text.Json;

namespace Loans.IntegrationTests;

public class OutboxDispatcherTests : IClassFixture<LoansApiFactory>, IAsyncLifetime
{
    private readonly LoansApiFactory factory;
    private readonly HttpClient client;

    public OutboxDispatcherTests(LoansApiFactory factory)
    {
        this.factory = factory;
        client = factory.CreateClient();
    }

    // Every test starts with an empty outbox and an external partner that answers 200.
    public async ValueTask InitializeAsync()
    {
        await factory.ExecuteSqlAsync("TRUNCATE outbox");
        factory.ExternalPartner.Reset();
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Theory]
    [InlineData(HttpStatusCode.OK)]
    [InlineData(HttpStatusCode.NoContent)]
    public async Task Any_2xx_marks_the_message_delivered(HttpStatusCode status)
    {
        var ssn = TestApi.NewSsn();
        var id = (await client.SubmitAsync(TestApi.Form(ssn, amount: 1500m))).ApplicationId!.Value;
        factory.ExternalPartner.Reset(status);

        await factory.DispatchOutboxAsync();

        var message = Assert.Single(await factory.OutboxAsync(id));
        Assert.NotNull(message.DeliveredAt);
        var call = Assert.Single(factory.ExternalPartner.Calls);
        Assert.Equal(HttpMethod.Put, call.Method);
        Assert.Equal($"/applications/{id}", call.Path);
        using var body = JsonDocument.Parse(call.Body);
        Assert.Equal(ssn, body.RootElement.GetProperty("ssn").GetString());
        Assert.Equal(1500m, body.RootElement.GetProperty("requestedAmount").GetDecimal());
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(null)] // Network error.
    public async Task A_failure_counts_an_attempt_and_the_tick_goes_on(HttpStatusCode? status)
    {
        var first = (await client.SubmitAsync(TestApi.Form(TestApi.NewSsn()))).ApplicationId!.Value;
        var second = (await client.SubmitAsync(TestApi.Form(TestApi.NewSsn()))).ApplicationId!.Value;
        factory.ExternalPartner.Reset(status);

        await factory.DispatchOutboxAsync();

        Assert.Equal(2, factory.ExternalPartner.Calls.Count);
        Assert.Equal(1, Assert.Single(await factory.OutboxAsync(first)).Attempts);
        Assert.Equal(1, Assert.Single(await factory.OutboxAsync(second)).Attempts);
    }

    [Fact]
    public async Task An_unreadable_row_fails_alone()
    {
        var brokenSsn = TestApi.NewSsn();
        var broken = (await client.SubmitAsync(TestApi.Form(brokenSsn))).ApplicationId!.Value;
        var healthy = (await client.SubmitAsync(TestApi.Form(TestApi.NewSsn()))).ApplicationId!.Value;
        var customer = await factory.FindCustomerAsync(brokenSsn);
        await factory.ExecuteSqlAsync($"UPDATE customers SET ssn_encrypted = '\\x00' WHERE id = '{customer!.Id}'");

        await factory.DispatchOutboxAsync();

        Assert.Equal(1, Assert.Single(await factory.OutboxAsync(broken)).Attempts);
        Assert.NotNull(Assert.Single(await factory.OutboxAsync(healthy)).DeliveredAt);
    }

    [Fact]
    public async Task After_ten_failed_attempts_the_message_is_dead_lettered()
    {
        var id = (await client.SubmitAsync(TestApi.Form(TestApi.NewSsn()))).ApplicationId!.Value;
        factory.ExternalPartner.Reset(HttpStatusCode.InternalServerError);

        for (var tick = 0; tick < 11; tick++)
            await factory.DispatchOutboxAsync();

        Assert.Equal(10, factory.ExternalPartner.Calls.Count); // The 11th tick leaves it alone.
        var message = Assert.Single(await factory.OutboxAsync(id));
        Assert.Equal(10, message.Attempts);
        Assert.Null(message.DeliveredAt);
    }

    [Fact]
    public async Task The_payload_carries_the_latest_data()
    {
        var ssn = TestApi.NewSsn();
        var id = (await client.SubmitAsync(TestApi.Form(ssn, state: "CA", amount: 1000m))).ApplicationId!.Value;
        await client.SubmitAsync(TestApi.Form(ssn, state: "TX", amount: 2500m));

        await factory.DispatchOutboxAsync();

        Assert.Equal(2, factory.ExternalPartner.Calls.Count);
        Assert.All(factory.ExternalPartner.Calls, call =>
        {
            using var body = JsonDocument.Parse(call.Body);
            Assert.Equal($"/applications/{id}", call.Path);
            Assert.Equal("TX", body.RootElement.GetProperty("state").GetString());
            Assert.Equal(2500m, body.RootElement.GetProperty("requestedAmount").GetDecimal());
        });
    }
}
