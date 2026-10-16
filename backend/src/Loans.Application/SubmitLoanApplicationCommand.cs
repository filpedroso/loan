namespace Loans.Application;

public sealed record SubmitLoanApplicationCommand(
    string FirstName,
    string LastName,
    string Address,
    string State,
    string CompanyName,
    string Ssn,
    decimal RequestedAmount);
