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

    [Fact]
    public async Task A_200_marks_the_message_delivered()
    {
        var ssn = TestApi.NewSsn();
        var id = (await client.SubmitAsync(TestApi.Form(ssn, amount: 1500m))).ApplicationId!.Value;

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
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(null)] // Network error.
    public async Task When_the_partner_looks_down_the_tick_stops_after_one_attempt(HttpStatusCode? status)
    {
        var first = (await client.SubmitAsync(TestApi.Form(TestApi.NewSsn()))).ApplicationId!.Value;
        var second = (await client.SubmitAsync(TestApi.Form(TestApi.NewSsn()))).ApplicationId!.Value;
        factory.ExternalPartner.Reset(status);

        await factory.DispatchOutboxAsync();

        Assert.Single(factory.ExternalPartner.Calls);
        Assert.Equal(1, Assert.Single(await factory.OutboxAsync(first)).Attempts);
        Assert.Equal(0, Assert.Single(await factory.OutboxAsync(second)).Attempts);
    }

    [Fact]
    public async Task A_400_counts_an_attempt_and_the_tick_goes_on()
    {
        var first = (await client.SubmitAsync(TestApi.Form(TestApi.NewSsn()))).ApplicationId!.Value;
        var second = (await client.SubmitAsync(TestApi.Form(TestApi.NewSsn()))).ApplicationId!.Value;
        factory.ExternalPartner.Reset(HttpStatusCode.BadRequest);

        await factory.DispatchOutboxAsync();

        Assert.Equal(2, factory.ExternalPartner.Calls.Count);
        Assert.Equal(1, Assert.Single(await factory.OutboxAsync(first)).Attempts);
        Assert.Equal(1, Assert.Single(await factory.OutboxAsync(second)).Attempts);
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
    public async Task One_tick_drains_more_than_one_batch()
    {
        // The test factory sets a batch size of 2.
        for (var i = 0; i < 5; i++)
            await client.SubmitAsync(TestApi.Form(TestApi.NewSsn()));

        await factory.DispatchOutboxAsync();

        Assert.Equal(5, factory.ExternalPartner.Calls.Count);
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
