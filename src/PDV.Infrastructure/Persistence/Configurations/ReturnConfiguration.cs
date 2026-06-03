using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PDV.Domain.Entities;

namespace PDV.Infrastructure.Persistence.Configurations;

public class ReturnConfiguration : IEntityTypeConfiguration<Return>
{
    public void Configure(EntityTypeBuilder<Return> entity)
    {
        entity.Property(e => e.Subtotal).HasPrecision(18, 2);
        entity.Property(e => e.TotalTax).HasPrecision(18, 2);
        entity.Property(e => e.TotalRefund).HasPrecision(18, 2);

        entity.HasOne(e => e.Shift)
              .WithMany()
              .HasForeignKey(e => e.ShiftId)
              .OnDelete(DeleteBehavior.Restrict);

        entity.OwnsMany(e => e.Taxes, a =>
        {
            a.ToTable("ReturnTaxBreakdowns");
            a.WithOwner().HasForeignKey("ReturnId");
            a.HasKey("Id");
            a.Property("Id").ValueGeneratedOnAdd();
            a.Property(x => x.BaseAmount).HasPrecision(18, 2);
            a.Property(x => x.TaxAmount).HasPrecision(18, 2);
            a.Property(x => x.Rate).HasPrecision(18, 4);
        });
    }
}
