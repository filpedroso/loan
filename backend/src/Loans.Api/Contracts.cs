using Loans.Domain.Decisions;
using Loans.UseCases;

namespace Loans.Api;

/// <summary>The form as JSON. Fields are nullable so that all validation happens in one place: the domain.</summary>
public sealed record SubmitLoanApplicationRequest(
    string? FirstName,
    string? LastName,
    string? Address,
    string? State,
    string? CompanyName,
    string? Ssn,
    decimal RequestedAmount)
{
    public SubmitLoanApplicationCommand ToCommand() => new(
        FirstName ?? "", LastName ?? "", Address ?? "", State ?? "", CompanyName ?? "", Ssn ?? "", RequestedAmount);
}

/// <summary>Reason codes shown to the applicant. Nobody is told they're on a blocklist; they're "not eligible".</summary>
public enum PublicReason
{
    StateNotServed,
    NotEligible,
}

/// <summary>The public JSON keeps the short name <c>applicationId</c>.</summary>
public sealed record SubmitLoanApplicationResponse(bool Approved, Guid? ApplicationId, PublicReason? Reason)
{
    public static SubmitLoanApplicationResponse From(SubmissionResult result) => new(
        result.IsApproved,
        result.LoanApplicationId,
        result.Reason is { } reason ? ToPublic(reason) : null);

    private static PublicReason ToPublic(DenialReason reason) => reason switch
    {
        DenialReason.StateNotServed => PublicReason.StateNotServed,
        DenialReason.SsnBlocklisted => PublicReason.NotEligible,
        _ => throw new ArgumentOutOfRangeException(nameof(reason), reason, "Unmapped denial reason."),
    };
}
