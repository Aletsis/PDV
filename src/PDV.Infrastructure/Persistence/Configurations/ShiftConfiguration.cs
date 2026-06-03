using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PDV.Domain.Entities;

namespace PDV.Infrastructure.Persistence.Configurations;

public class ShiftConfiguration : IEntityTypeConfiguration<Shift>
{
    public void Configure(EntityTypeBuilder<Shift> entity)
    {
        entity.Property(e => e.InitialCash).HasPrecision(18, 2);
        entity.Property(e => e.SystemExpectedCash).HasPrecision(18, 2);
        entity.Property(e => e.TotalCashReturns).HasPrecision(18, 2);

        entity.OwnsMany(e => e.PaymentMethodTotals, a =>
        {
            a.WithOwner().HasForeignKey("ShiftId");
            a.HasKey("Id");
            a.Property("Id").ValueGeneratedOnAdd();
            a.Property(x => x.Amount).HasPrecision(18, 2);
        });

        entity.OwnsMany(e => e.SalesTaxTotals, a =>
        {
            a.WithOwner().HasForeignKey("ShiftId");
            a.HasKey("Id");
            a.Property("Id").ValueGeneratedOnAdd();
            a.Property(x => x.BaseAmount).HasPrecision(18, 2);
            a.Property(x => x.TaxAmount).HasPrecision(18, 2);
        });

        entity.OwnsMany(e => e.ReturnsTaxTotals, a =>
        {
            a.WithOwner().HasForeignKey("ShiftId");
            a.HasKey("Id");
            a.Property("Id").ValueGeneratedOnAdd();
            a.Property(x => x.BaseAmount).HasPrecision(18, 2);
            a.Property(x => x.TaxAmount).HasPrecision(18, 2);
        });

        entity.OwnsMany(e => e.CreditNotes, a =>
        {
            a.WithOwner().HasForeignKey("ShiftId");
            a.HasKey("Id");
            a.Property("Id").ValueGeneratedOnAdd();
            a.Property(x => x.Amount).HasPrecision(18, 2);
        });
    }
}
