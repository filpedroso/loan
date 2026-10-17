using Loans.Domain.Decisions;
using Loans.UseCases;

namespace Loans.Api;

/// <summary>The form as JSON. Fields are nullable so that all validation happens in one place: the domain.</summary>
public sealed record SubmitApplicationRequest(
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

public enum Outcome
{
    Approved,
    Denied,
}

/// <summary>
/// Reason codes shown to the applicant. The blocklist stays generic, so the API can't be used to test
/// SSNs against it.
/// </summary>
public enum PublicReason
{
    StateNotServed,
    NotEligible,
}

public sealed record SubmitApplicationResponse(Outcome Decision, Guid? ApplicationId, IReadOnlyList<PublicReason> Reasons)
{
    public static SubmitApplicationResponse From(SubmissionResult result) => new(
        result.IsApproved ? Outcome.Approved : Outcome.Denied,
        result.ApplicationId,
        result.Reasons.Select(ToPublic).ToList());

    private static PublicReason ToPublic(DenialReason reason) => reason switch
    {
        DenialReason.StateNotServed => PublicReason.StateNotServed,
        DenialReason.SsnBlocklisted => PublicReason.NotEligible,
        _ => throw new ArgumentOutOfRangeException(nameof(reason), reason, "Unmapped denial reason."),
    };
}
