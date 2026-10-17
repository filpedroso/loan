using System.Net;
using System.Text;
using System.Text.Json;
using Loans.Domain.Customers;
using Loans.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Loans.IntegrationTests;

public class SubmitLoanApplicationTests(LoansApiFactory factory) : IClassFixture<LoansApiFactory>
{
    private readonly HttpClient client = factory.CreateClient();

    [Fact]
    public async Task A_new_applicant_is_approved_and_saved_with_an_outbox_message()
    {
        var ssn = TestApi.NewSsn();

        var response = await client.SubmitAsync(TestApi.Form(ssn, amount: 1500m));

        Assert.True(response.Approved);
        Assert.Null(response.Reason);
        var customer = await factory.FindCustomerAsync(ssn);
        Assert.NotNull(customer);
        Assert.Equal(Ssn.Parse(ssn), customer.Ssn);
        Assert.Equal("CA", customer.State);
        Assert.Equal(response.ApplicationId, customer.LoanApplication?.Id);
        Assert.Equal(RequestedAmount.Of(1500m), customer.LoanApplication?.RequestedAmount);
        var message = Assert.Single(await factory.OutboxAsync(response.ApplicationId!.Value));
        Assert.Equal(0, message.Attempts);
        Assert.Null(message.DeliveredAt);
    }

    [Fact]
    public async Task The_ssn_is_not_stored_in_plain_text()
    {
        var ssn = TestApi.NewSsn();
        await client.SubmitAsync(TestApi.Form(ssn));
        var customer = await factory.FindCustomerAsync(ssn);

        await using var scope = factory.Services.CreateAsyncScope();
        var stored = await scope.ServiceProvider.GetRequiredService<LoansDbContext>().Database
            .SqlQuery<byte[]>($"SELECT ssn_encrypted AS \"Value\" FROM customers WHERE id = {customer!.Id}")
            .SingleAsync(TestContext.Current.CancellationToken);

        Assert.DoesNotContain(ssn, Encoding.ASCII.GetString(stored));
    }

    [Fact]
    public async Task A_returning_customer_is_overwritten_and_keeps_the_same_ids()
    {
        var ssn = TestApi.NewSsn();
        var first = await client.SubmitAsync(TestApi.Form(ssn, state: "CA", amount: 1500m));

        var second = await client.SubmitAsync(TestApi.Form(ssn, state: "TX", amount: 2500m, firstName: "Janet"));

        Assert.True(second.Approved);
        Assert.Equal(first.ApplicationId, second.ApplicationId);
        var customer = await factory.FindCustomerAsync(ssn);
        Assert.Equal(("Janet", "TX"), (customer!.FirstName, customer.State));
        Assert.Equal(RequestedAmount.Of(2500m), customer.LoanApplication?.RequestedAmount);
        Assert.Equal(2, (await factory.OutboxAsync(first.ApplicationId!.Value)).Count);
    }

    [Theory]
    [InlineData(null, "NY", "StateNotServed")]
    [InlineData("987-65-4329", "CA", "NotEligible")]
    [InlineData("987-65-4328", "NY", "StateNotServed")] // Blocklisted too, but the SSN is never checked.
    public async Task A_denied_application_is_explained_and_writes_nothing(string? ssn, string state, string reason)
    {
        ssn ??= TestApi.NewSsn();

        var response = await client.SubmitAsync(TestApi.Form(ssn, state));

        Assert.False(response.Approved);
        Assert.Null(response.ApplicationId);
        Assert.Equal(reason, response.Reason);
        Assert.Null(await factory.FindCustomerAsync(ssn));
    }

    [Theory]
    [InlineData("123", 1500, "ssn")]
    [InlineData("666-12-3456", 0, "requestedAmount")]
    public async Task Invalid_input_returns_400_naming_the_field(string ssn, decimal amount, string field)
    {
        var response = await client.PostAsync(TestApi.Form(ssn, amount: amount));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.True(problem.RootElement.GetProperty("errors").TryGetProperty(field, out _));
    }

    [Fact]
    public async Task A_failure_while_saving_writes_nothing()
    {
        var ssn = TestApi.NewSsn();
        await factory.ExecuteSqlAsync("""
            CREATE FUNCTION fail_outbox() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RAISE EXCEPTION 'simulated failure'; END $$;
            CREATE TRIGGER fail_outbox BEFORE INSERT ON outbox FOR EACH ROW EXECUTE FUNCTION fail_outbox();
            """);
        try
        {
            var response = await client.PostAsync(TestApi.Form(ssn));

            Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
            Assert.Null(await factory.FindCustomerAsync(ssn));
        }
        finally
        {
            await factory.ExecuteSqlAsync("DROP TRIGGER fail_outbox ON outbox; DROP FUNCTION fail_outbox();");
        }
    }

    [Fact]
    public async Task Simultaneous_first_submissions_with_the_same_ssn_leave_one_customer()
    {
        var ssn = TestApi.NewSsn();

        var responses = await Task.WhenAll(
            Enumerable.Range(1, 10).Select(i => client.SubmitAsync(TestApi.Form(ssn, amount: 1000m + i))));

        Assert.All(responses, r => Assert.True(r.Approved));
        var loanApplicationId = Assert.Single(responses.Select(r => r.ApplicationId).Distinct());
        Assert.NotNull(await factory.FindCustomerAsync(ssn));
        Assert.Equal(10, (await factory.OutboxAsync(loanApplicationId!.Value)).Count);
    }
}
