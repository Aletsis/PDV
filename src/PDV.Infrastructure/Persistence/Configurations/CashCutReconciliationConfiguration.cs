using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PDV.Domain.Entities;

namespace PDV.Infrastructure.Persistence.Configurations;

public class CashCutReconciliationConfiguration : IEntityTypeConfiguration<CashCutReconciliation>
{
    public void Configure(EntityTypeBuilder<CashCutReconciliation> entity)
    {
        entity.ToTable("CashCutReconciliations");

        entity.Property(e => e.InitialCash).HasPrecision(18, 2);
        entity.Property(e => e.CashSalesTotal).HasPrecision(18, 2);
        entity.Property(e => e.CardSalesTotal).HasPrecision(18, 2);
        entity.Property(e => e.InflowsTotal).HasPrecision(18, 2);
        entity.Property(e => e.OutflowsTotal).HasPrecision(18, 2);
        entity.Property(e => e.ReturnsTotal).HasPrecision(18, 2);
        entity.Property(e => e.ExpectedCash).HasPrecision(18, 2);
        entity.Property(e => e.ExpectedCardVouchers).HasPrecision(18, 2);
        entity.Property(e => e.DeliveredCash).HasPrecision(18, 2);
        entity.Property(e => e.DeliveredCardVouchers).HasPrecision(18, 2);
        entity.Property(e => e.CashDifference).HasPrecision(18, 2);
        entity.Property(e => e.CardVouchersDifference).HasPrecision(18, 2);

        entity.Property(e => e.CashierUserId).HasMaxLength(450);
        entity.Property(e => e.ReconciledByUserId).HasMaxLength(450);
        entity.Property(e => e.Notes).HasMaxLength(1000);
        entity.Property(e => e.Status).HasConversion<int>();

        entity.HasOne(e => e.Shift)
              .WithMany()
              .HasForeignKey(e => e.ShiftId)
              .OnDelete(DeleteBehavior.Restrict);

        entity.HasOne(e => e.CashCut)
              .WithMany()
              .HasForeignKey(e => e.CashCutId)
              .OnDelete(DeleteBehavior.SetNull);

        entity.HasOne(e => e.CashRegister)
              .WithMany()
              .HasForeignKey(e => e.CashRegisterId)
              .OnDelete(DeleteBehavior.Restrict);

        entity.OwnsMany(e => e.CashDenominations, a =>
        {
            a.WithOwner().HasForeignKey("ReconciliationId");
            a.ToTable("CashCutReconciliation_Denominations");
            a.HasKey("Id");
            a.Property("Id").ValueGeneratedOnAdd();
            a.Property(x => x.Type).HasConversion<int>();
        });
    }
}
