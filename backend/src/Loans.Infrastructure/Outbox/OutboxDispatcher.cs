using System.Net;
using Loans.Domain.Customers;
using Loans.Infrastructure.ExternalPartner;
using Loans.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Loans.Infrastructure.Outbox;

/// <summary>
/// One worker tick: sends every pending message to the external partner in batches, each message at most once per tick.
/// When the external partner looks down (500, timeout, network error) the tick stops, so the remaining messages wait
/// for the next tick instead of using up their attempts during an outage.
/// </summary>
public sealed class OutboxDispatcher(
    LoansDbContext db, ExternalPartnerClient partner, IOptions<OutboxOptions> options, ILogger<OutboxDispatcher> logger)
{
    public async Task DispatchAsync(CancellationToken ct)
    {
        var after = Guid.Empty;
        while (await DispatchBatchAsync(after, ct) is { } lastId)
            after = lastId;
    }

    /// <summary>Sends one batch. Returns the id of its last message, or null when the tick should end.</summary>
    private async Task<Guid?> DispatchBatchAsync(Guid after, CancellationToken ct)
    {
        var settings = options.Value;
        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        // SKIP LOCKED: replicas never claim the same message. Ids are UUIDv7, so id order is creation order,
        // and "id > after" keeps a tick from retrying what it already tried.
        var batch = await db.Outbox.FromSql($"""
            SELECT * FROM outbox
            WHERE delivered_at IS NULL AND attempts < {settings.MaxAttempts} AND id > {after}
            ORDER BY id
            LIMIT {settings.BatchSize}
            FOR UPDATE SKIP LOCKED
            """).ToListAsync(ct);
        if (batch.Count == 0)
            return null;

        var loanApplicationIds = batch.Select(m => m.LoanApplicationId).ToList();
        var customers = await db.Customers.AsNoTracking()
            .Include(c => c.LoanApplication)
            .Where(c => loanApplicationIds.Contains(c.LoanApplication!.Id))
            .ToDictionaryAsync(c => c.LoanApplication!.Id, ct);

        var partnerDown = false;
        foreach (var message in batch)
        {
            var (delivered, looksDown, status) = await SendAsync(customers[message.LoanApplicationId], ct);
            if (delivered)
            {
                message.DeliveredAt = DateTimeOffset.UtcNow;
                logger.LogInformation("Delivered loan application {LoanApplicationId} to the external partner.", message.LoanApplicationId);
                continue;
            }

            message.Attempts++;
            if (message.Attempts >= settings.MaxAttempts)
            {
                logger.LogError(
                    "Delivery of loan application {LoanApplicationId} to the external partner failed ({Status}) {Attempts} times; dead-lettered.",
                    message.LoanApplicationId, status, message.Attempts);
            }
            else
            {
                logger.LogWarning(
                    "Delivery of loan application {LoanApplicationId} to the external partner failed ({Status}), attempt {Attempt} of {MaxAttempts}.",
                    message.LoanApplicationId, status, message.Attempts, settings.MaxAttempts);
            }

            if (looksDown)
            {
                partnerDown = true;
                break;
            }
        }

        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        db.ChangeTracker.Clear();
        return partnerDown ? null : batch[^1].Id;
    }

    private async Task<(bool Delivered, bool LooksDown, string Status)> SendAsync(Customer customer, CancellationToken ct)
    {
        try
        {
            var status = await partner.PutLoanApplicationAsync(customer, ct);
            return (status == HttpStatusCode.OK, (int)status >= 500, ((int)status).ToString());
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !ct.IsCancellationRequested)
        {
            return (false, true, ex is TaskCanceledException ? "timeout" : "network error");
        }
    }
}
