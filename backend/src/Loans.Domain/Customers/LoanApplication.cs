namespace Loans.Domain.Customers;

/// <summary>
/// A customer's loan application. Only <see cref="Customer.Apply"/> creates or changes it,
/// so an application can't exist without its owner.
/// </summary>
public sealed class LoanApplication
{
    private LoanApplication() { } // For EF Core.

    internal LoanApplication(Guid customerId, Money requestedAmount)
    {
        Id = Guid.CreateVersion7();
        CustomerId = customerId;
        RequestedAmount = requestedAmount;
    }

    public Guid Id { get; private set; }
    public Guid CustomerId { get; private set; }
    public Money RequestedAmount { get; private set; } = null!;

    internal void Overwrite(Money requestedAmount) => RequestedAmount = requestedAmount;
}
