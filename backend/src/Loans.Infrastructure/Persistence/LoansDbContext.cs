using Loans.Domain;
using Loans.Domain.Customers;
using Loans.Infrastructure.Outbox;
using Loans.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;

namespace Loans.Infrastructure.Persistence;

/// <summary>Maps the domain entities directly. Table and column names are snake_case.</summary>
public sealed class LoansDbContext(DbContextOptions<LoansDbContext> options, SsnProtector ssnProtector)
    : DbContext(options)
{
    /// <summary>Shadow property: the SSN hash used for lookups. The domain never sees it.</summary>
    public const string SsnHash = "SsnHash";

    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<LoanApplication> Applications => Set<LoanApplication>();
    public DbSet<BlockedSsn> Blocklist => Set<BlockedSsn>();
    public DbSet<OutboxMessage> Outbox => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        // EF caches the model, so the converter must capture only the (singleton) protector, not this context.
        var protector = ssnProtector;

        model.Entity<Customer>(customer =>
        {
            // Ids are created in the domain. Without this, EF would treat a new application
            // reached through customer.Application as an existing row and UPDATE it.
            customer.Property(c => c.Id).ValueGeneratedNever();
            customer.Property(c => c.Ssn)
                .HasColumnName("ssn_encrypted")
                .HasConversion(ssn => protector.Encrypt(ssn), bytes => protector.Decrypt(bytes));
            customer.Property<byte[]>(SsnHash).IsRequired();
            customer.HasIndex(SsnHash).IsUnique();
            customer.Property(c => c.FirstName).HasMaxLength(100);
            customer.Property(c => c.LastName).HasMaxLength(100);
            customer.Property(c => c.Address).HasMaxLength(300);
            customer.Property(c => c.State).HasMaxLength(2);
            customer.Property(c => c.CompanyName).HasMaxLength(200);
            customer.HasOne(c => c.Application).WithOne().HasForeignKey<LoanApplication>(a => a.CustomerId);
        });

        model.Entity<LoanApplication>(application =>
        {
            application.Property(a => a.Id).ValueGeneratedNever();
            application.Property(a => a.RequestedAmount)
                .HasConversion(money => money.Amount, amount => Money.Of(amount, nameof(LoanApplication.RequestedAmount)))
                .HasPrecision(18, 2);
        });

        model.Entity<BlockedSsn>(blocked => blocked.HasKey(b => b.SsnHash));

        model.Entity<OutboxMessage>(message =>
        {
            message.Property(m => m.Id).ValueGeneratedNever();
            message.HasOne<LoanApplication>().WithMany().HasForeignKey(m => m.ApplicationId);
            // Keeps the worker's query small however many messages were already delivered.
            message.HasIndex(m => m.Id).HasFilter("delivered_at IS NULL").HasDatabaseName("ix_outbox_pending");
        });
    }
}
