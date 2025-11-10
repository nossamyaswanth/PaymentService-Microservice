using Microsoft.EntityFrameworkCore;
using PaymentService.Models;

namespace PaymentService.Data;

public class PaymentsDb : DbContext
{
    public PaymentsDb(DbContextOptions<PaymentsDb> options) : base(options) { }

    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<IdempotencyKey> IdempotencyKeys => Set<IdempotencyKey>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Payment>().ToTable("Payments").HasKey(p => p.PaymentId);
        modelBuilder.Entity<Payment>()
            .Property(p => p.Amount)
            .HasPrecision(18, 2);
        modelBuilder.Entity<IdempotencyKey>().ToTable("IdempotencyKeys").HasKey(i => i.Key);
    }
}