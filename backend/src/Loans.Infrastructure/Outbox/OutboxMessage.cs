namespace Loans.Infrastructure.Outbox;

/// <summary>
/// A CRM sync waiting to happen. It only references the loan application: the worker builds the payload from
/// current data when it sends, so the SSN is never copied here and the CRM always gets the latest data.
/// </summary>
public sealed class OutboxMessage
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public required Guid LoanApplicationId { get; init; }
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public int Attempts { get; set; }

    /// <summary>Set when the CRM answers 200. Null means not delivered yet.</summary>
    public DateTimeOffset? DeliveredAt { get; set; }
}
