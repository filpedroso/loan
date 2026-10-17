namespace Loans.Domain.Customers;

/// <summary>
/// Aggregate root. A customer is identified by their SSN and owns at most one loan application,
/// which every new submission overwrites.
/// </summary>
public sealed class Customer
{
    private Customer() { } // For EF Core.

    public Guid Id { get; private set; }
    public Ssn Ssn { get; private set; } = null!;
    public string FirstName { get; private set; } = "";
    public string LastName { get; private set; } = "";
    public string Address { get; private set; } = "";
    public string State { get; private set; } = "";
    public string CompanyName { get; private set; } = "";
    public LoanApplication? LoanApplication { get; private set; }

    public static Customer Register(
        Ssn ssn, string firstName, string lastName, string address, string state, string companyName)
    {
        var customer = new Customer { Id = Guid.CreateVersion7(), Ssn = ssn };
        customer.UpdateDetails(firstName, lastName, address, state, companyName);
        return customer;
    }

    /// <summary>Overwrites the details with the latest submission. Nothing changes if any field is invalid.</summary>
    public void UpdateDetails(string firstName, string lastName, string address, string state, string companyName)
    {
        var validFirstName = Required(firstName, nameof(FirstName), 100);
        var validLastName = Required(lastName, nameof(LastName), 100);
        var validAddress = Required(address, nameof(Address), 300);
        var validState = UsStates.Normalize(state)
            ?? throw new ValidationException(nameof(State), "Must be a US state code.");
        var validCompanyName = Required(companyName, nameof(CompanyName), 200);

        (FirstName, LastName, Address, State, CompanyName) =
            (validFirstName, validLastName, validAddress, validState, validCompanyName);
    }

    /// <summary>Creates the loan application, or overwrites its amount if there is one. Its id never changes.</summary>
    public LoanApplication Apply(RequestedAmount requestedAmount)
    {
        if (LoanApplication is null)
            LoanApplication = new LoanApplication(Id, requestedAmount);
        else
            LoanApplication.Overwrite(requestedAmount);

        return LoanApplication;
    }

    private static string Required(string? value, string field, int maxLength)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrEmpty(trimmed))
            throw new ValidationException(field, "Is required.");
        if (trimmed.Length > maxLength)
            throw new ValidationException(field, $"Must be at most {maxLength} characters.");
        if (trimmed.Any(char.IsControl)) // PostgreSQL rejects NUL, and no field needs line breaks or tabs.
            throw new ValidationException(field, "Can't contain control characters.");

        return trimmed;
    }
}
