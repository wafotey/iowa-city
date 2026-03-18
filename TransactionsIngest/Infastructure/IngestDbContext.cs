using Microsoft.EntityFrameworkCore;

namespace TransactionsIngest.Data;

public sealed class IngestDbContext : DbContext
{
    public IngestDbContext(DbContextOptions<IngestDbContext> options)
        : base(options) { }

    public DbSet<Transaction> Transactions => Set<Transaction>();
    public DbSet<TransactionAudit> TransactionAudits => Set<TransactionAudit>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Transaction>(e =>
        {
            e.ToTable("Transactions");
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.TransactionId);
            e.Property(x => x.CardLast4).HasMaxLength(4);
            e.Property(x => x.LocationCode).HasMaxLength(20);
            e.Property(x => x.ProductName).HasMaxLength(20);
            e.Property(x => x.Amount).HasPrecision(18, 2);
            e.HasMany(x => x.AuditEntries).WithOne(x => x.Transaction).HasForeignKey(x => x.TransactionEntityId);
        });

        modelBuilder.Entity<TransactionAudit>(e =>
        {
            e.ToTable("TransactionAudits");
            e.HasKey(x => x.Id);
            e.Property(x => x.ChangeType).HasMaxLength(20);
            e.Property(x => x.ChangedFields).HasMaxLength(500);
            e.Property(x => x.BeforeChanges).HasMaxLength(4000);
            e.Property(x => x.AfterChanges).HasMaxLength(4000);
        });
    }
}
