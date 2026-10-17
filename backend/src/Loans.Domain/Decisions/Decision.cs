namespace Loans.Domain.Decisions;

/// <summary>Approved, or denied for one reason.</summary>
public sealed record Decision(DenialReason? Reason)
{
    public bool Approved => Reason is null;
}
