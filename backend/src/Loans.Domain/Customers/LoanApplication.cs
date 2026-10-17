namespace Loans.Domain.Customers;

/// <summary>
/// A customer's loan application. Only <see cref="Customer.Apply"/> creates or changes it,
/// so an application can't exist without its owner.
/// </summary>
public sealed class LoanApplication
{
    private LoanApplication() { } // For EF Core.

    internal LoanApplication(Guid customerId, RequestedAmount requestedAmount)
    {
        Id = Guid.CreateVersion7();
        CustomerId = customerId;
        RequestedAmount = requestedAmount;
    }

    public Guid Id { get; private set; }
    public Guid CustomerId { get; private set; }
    public RequestedAmount RequestedAmount { get; private set; } = null!;

    internal void Overwrite(RequestedAmount requestedAmount) => RequestedAmount = requestedAmount;
}
