using Loans.Domain;
using Loans.Domain.Customers;
using Loans.Infrastructure.Outbox;
using Loans.Infrastructure.Security;
using Loans.UseCases;
using Microsoft.EntityFrameworkCore;

namespace Loans.Infrastructure.Persistence;

public sealed class CustomerRepository(LoansDbContext db, SsnProtector ssnProtector) : ICustomerRepository
{
    public async Task<Customer?> FindBySsnAsync(Ssn ssn, CancellationToken ct)
    {
        var hash = ssnProtector.Hash(ssn);

        // The transaction lasts until SaveApprovedAsync commits it, or the request ends and it rolls back.
        // The advisory lock, keyed on the SSN hash, lives as long as the transaction: submissions for the
        // same SSN take turns, so the second one finds the customer the first one created and updates it.
        await db.Database.BeginTransactionAsync(ct);
        await db.Database.ExecuteSqlAsync($"SELECT pg_advisory_xact_lock({BitConverter.ToInt64(hash)})", ct);

        return await db.Customers
            .Include(c => c.Application)
            .SingleOrDefaultAsync(c => EF.Property<byte[]>(c, LoansDbContext.SsnHash) == hash, ct);
    }

    public async Task SaveApprovedAsync(Customer customer, CancellationToken ct)
    {
        var application = customer.Application
            ?? throw new InvalidOperationException("An approved customer must have an application.");

        if (db.Entry(customer).State == EntityState.Detached)
        {
            db.Customers.Add(customer);
            db.Entry(customer).Property(LoansDbContext.SsnHash).CurrentValue = ssnProtector.Hash(customer.Ssn);
        }

        db.Outbox.Add(new OutboxMessage { ApplicationId = application.Id });

        // One SaveChanges writes customer, application and outbox row; any failure rolls all of them back.
        await db.SaveChangesAsync(ct);
        if (db.Database.CurrentTransaction is { } transaction)
            await transaction.CommitAsync(ct);
    }
}
