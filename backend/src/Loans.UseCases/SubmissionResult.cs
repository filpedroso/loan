using Loans.Domain.Decisions;

namespace Loans.UseCases;

public sealed record SubmissionResult(Guid? LoanApplicationId, DenialReason? Reason)
{
    public bool IsApproved => LoanApplicationId is not null;

    public static SubmissionResult Approved(Guid loanApplicationId) => new(loanApplicationId, null);

    public static SubmissionResult Denied(DenialReason reason) => new(null, reason);
}
