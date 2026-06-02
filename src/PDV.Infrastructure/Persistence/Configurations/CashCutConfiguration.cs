using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PDV.Domain.Entities;

namespace PDV.Infrastructure.Persistence.Configurations;

public class CashCutConfiguration : IEntityTypeConfiguration<CashCut>
{
    public void Configure(EntityTypeBuilder<CashCut> entity)
    {
        entity.Property(e => e.SystemExpectedCash).HasPrecision(18, 2);
        entity.Property(e => e.DeclaredPhysicalCash).HasPrecision(18, 2);
        entity.Property(e => e.DeclaredVouchersTotal).HasPrecision(18, 2);
        entity.Property(e => e.Difference).HasPrecision(18, 2);

        entity.HasOne(e => e.Employee)
              .WithMany()
              .HasForeignKey(e => e.EmployeeId)
              .OnDelete(DeleteBehavior.Restrict);

        entity.HasOne(e => e.CashRegister)
              .WithMany()
              .HasForeignKey(e => e.CashRegisterId)
              .OnDelete(DeleteBehavior.Restrict);

        entity.OwnsMany(e => e.CashDenominations, a =>
        {
            a.WithOwner().HasForeignKey("CashCutId");
            a.Property(x => x.Type).HasConversion<int>();
        });

        entity.OwnsMany(e => e.DeclaredVouchers, a =>
        {
            a.WithOwner().HasForeignKey("CashCutId");
            a.Property(x => x.PaymentMethod).HasConversion<int>();
            a.Property(x => x.Amount).HasPrecision(18, 2);
        });
    }
}
