using Loans.Infrastructure.ExternalPartner;
using Loans.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Loans.Infrastructure.Outbox;

/// <summary>
/// One worker tick: sends up to <see cref="BatchSize"/> pending messages to the external partner, oldest first.
/// Every failure costs the message an attempt; after <see cref="MaxAttempts"/> it's dead-lettered.
/// Only one worker runs, so messages aren't locked.
/// </summary>
public sealed class OutboxDispatcher(LoansDbContext db, ExternalPartnerClient partner, ILogger<OutboxDispatcher> logger)
{
    public const int BatchSize = 100;
    public const int MaxAttempts = 10;

    public async Task DispatchAsync(CancellationToken ct)
    {
        // Ids are UUIDv7, so id order is creation order.
        var batch = await db.Outbox
            .Where(m => m.DeliveredAt == null && m.Attempts < MaxAttempts)
            .OrderBy(m => m.Id)
            .Take(BatchSize)
            .ToListAsync(ct);

        foreach (var message in batch)
        {
            var failure = await SendAsync(message, ct);
            if (failure is null)
            {
                message.DeliveredAt = DateTimeOffset.UtcNow;
                logger.LogInformation(
                    "Delivered loan application {LoanApplicationId} to the external partner.", message.LoanApplicationId);
                continue;
            }

            message.Attempts++;
            if (message.Attempts >= MaxAttempts)
            {
                logger.LogError(
                    "Delivery of loan application {LoanApplicationId} to the external partner failed ({Failure}) {Attempts} times; dead-lettered.",
                    message.LoanApplicationId, failure, message.Attempts);
            }
            else
            {
                logger.LogWarning(
                    "Delivery of loan application {LoanApplicationId} to the external partner failed ({Failure}), attempt {Attempt} of {MaxAttempts}.",
                    message.LoanApplicationId, failure, message.Attempts, MaxAttempts);
            }
        }

        await db.SaveChangesAsync(ct);
    }

    /// <summary>Returns null when the partner accepted the message, otherwise what went wrong.</summary>
    private async Task<string?> SendAsync(OutboxMessage message, CancellationToken ct)
    {
        try
        {
            // Loaded per message, from current data: the partner gets the latest submission, and one unreadable
            // row fails alone instead of failing the whole batch.
            var customer = await db.Customers.AsNoTracking()
                .Include(c => c.LoanApplication)
                .SingleAsync(c => c.LoanApplication!.Id == message.LoanApplicationId, ct);
            var status = (int)await partner.PutLoanApplicationAsync(customer, ct);
            return status is >= 200 and < 300 ? null : $"HTTP {status}";
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            return ex.Message;
        }
    }
}
