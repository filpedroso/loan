namespace Loans.Infrastructure.Persistence;

/// <summary>A blocklisted SSN, stored only as its hash.</summary>
public sealed class BlockedSsn
{
    public required byte[] SsnHash { get; init; }
}
