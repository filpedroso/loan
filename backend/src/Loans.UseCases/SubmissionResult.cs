using Loans.Domain.Decisions;

namespace Loans.UseCases;

public sealed record SubmissionResult(Guid? ApplicationId, IReadOnlyList<DenialReason> Reasons)
{
    public bool IsApproved => ApplicationId is not null;

    public static SubmissionResult Approved(Guid applicationId) => new(applicationId, []);

    public static SubmissionResult Denied(IReadOnlyList<DenialReason> reasons) => new(null, reasons);
}
