using Loans.Domain.Decisions;

namespace Loans.UseCases;

public sealed record SubmissionResult(Guid? LoanApplicationId, IReadOnlyList<DenialReason> Reasons)
{
    public bool IsApproved => LoanApplicationId is not null;

    public static SubmissionResult Approved(Guid loanApplicationId) => new(loanApplicationId, []);

    public static SubmissionResult Denied(IReadOnlyList<DenialReason> reasons) => new(null, reasons);
}
