using Loans.Domain.Customers;
using Loans.Infrastructure.Outbox;
using Loans.Infrastructure.Security;
using Loans.UseCases;
using Microsoft.EntityFrameworkCore;

namespace Loans.Infrastructure.Persistence;

public sealed class CustomerRepository(LoansDbContext db, SsnProtector ssnProtector) : ICustomerRepository
{
    public async Task<Customer?> LockAndFindBySsnAsync(Ssn ssn, CancellationToken ct)
    {
        var hash = ssnProtector.Hash(ssn);

        // The transaction lasts until SaveAsync commits it, or the request ends and it rolls back.
        // The advisory lock, keyed on the SSN hash, lives as long as the transaction: submissions for the
        // same SSN take turns, so the second one finds the customer the first one created and updates it.
        await db.Database.BeginTransactionAsync(ct);
        // A failed save abandons the whole transaction, so EF's automatic savepoint would only add round trips.
        db.Database.AutoSavepointsEnabled = false;
        await db.Database.ExecuteSqlAsync($"SELECT pg_advisory_xact_lock({BitConverter.ToInt64(hash)})", ct);

        return await db.Customers
            .Include(c => c.LoanApplication)
            .SingleOrDefaultAsync(c => EF.Property<byte[]>(c, LoansDbContext.SsnHash) == hash, ct);
    }

    public async Task SaveAsync(Customer customer, CancellationToken ct)
    {
        var transaction = db.Database.CurrentTransaction
            ?? throw new InvalidOperationException($"Call {nameof(LockAndFindBySsnAsync)} before {nameof(SaveAsync)}.");
        var loanApplication = customer.LoanApplication
            ?? throw new InvalidOperationException("A saved customer must have a loan application.");

        if (db.Entry(customer).State == EntityState.Detached)
        {
            db.Customers.Add(customer);
            db.Entry(customer).Property(LoansDbContext.SsnHash).CurrentValue = ssnProtector.Hash(customer.Ssn);
        }

        // Every saved loan application is synced to the external partner.
        db.Outbox.Add(new OutboxMessage { LoanApplicationId = loanApplication.Id });

        // One SaveChanges writes customer, loan application and outbox row; any failure rolls all of them back.
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }
}
