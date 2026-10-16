namespace Loans.Domain.Decisions;

/// <summary>Port: the list of SSNs we don't lend to. Implemented by Infrastructure.</summary>
public interface ISsnBlocklist
{
    Task<bool> ContainsAsync(Ssn ssn, CancellationToken ct);
}
