using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PDV.Domain.Entities;
using PDV.Domain.ValueObjects;

namespace PDV.Infrastructure.Persistence.Configurations;

public class SaleConfiguration : IEntityTypeConfiguration<Sale>
{
    public void Configure(EntityTypeBuilder<Sale> entity)
    {
        entity.Property(e => e.TotalAmount).HasPrecision(18, 2);
        entity.Property(e => e.SaleNumber).HasMaxLength(50);

        entity.OwnsMany(e => e.Taxes, a =>
        {
            a.ToTable("SaleTaxBreakdowns");
            a.WithOwner().HasForeignKey("SaleId");
            a.Property(x => x.BaseAmount).HasPrecision(18, 2);
            a.Property(x => x.TaxAmount).HasPrecision(18, 2);
            a.Property(x => x.Rate).HasPrecision(18, 4);
        });

        entity.HasOne(e => e.CashRegister)
              .WithMany()
              .HasForeignKey(e => e.CashRegisterId)
              .OnDelete(DeleteBehavior.SetNull);

        entity.HasOne(e => e.Client)
              .WithMany()
              .HasForeignKey(e => e.ClientId)
              .OnDelete(DeleteBehavior.SetNull);

        entity.HasOne(e => e.Branch)
              .WithMany()
              .HasForeignKey(e => e.BranchId)
              .OnDelete(DeleteBehavior.Restrict);
    }
}
