namespace Loans.Domain.Decisions;

public sealed record Decision(IReadOnlyList<DenialReason> Reasons)
{
    public bool Approved => Reasons.Count == 0;
}
