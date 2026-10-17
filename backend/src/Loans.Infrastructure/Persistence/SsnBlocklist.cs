using Loans.Domain.Customers;
using Loans.Domain.Decisions;
using Loans.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;

namespace Loans.Infrastructure.Persistence;

public sealed class SsnBlocklist(LoansDbContext db, SsnProtector ssnProtector) : ISsnBlocklist
{
    public Task<bool> ContainsAsync(Ssn ssn, CancellationToken ct)
    {
        var hash = ssnProtector.Hash(ssn);
        return db.Blocklist.AnyAsync(b => b.SsnHash == hash, ct);
    }
}
