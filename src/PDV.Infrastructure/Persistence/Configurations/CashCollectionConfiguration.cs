using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PDV.Domain.Entities;

namespace PDV.Infrastructure.Persistence.Configurations;

public class CashCollectionConfiguration : IEntityTypeConfiguration<CashCollection>
{
    public void Configure(EntityTypeBuilder<CashCollection> entity)
    {
        entity.Property(e => e.Amount).HasPrecision(18, 2);

        entity.HasOne(e => e.CashRegister)
              .WithMany()
              .HasForeignKey(e => e.CashRegisterId)
              .OnDelete(DeleteBehavior.Restrict);

        entity.OwnsMany(e => e.Denominations, a =>
        {
            a.WithOwner().HasForeignKey("CashCollectionId");
            a.Property(x => x.Type).HasConversion<int>();
        });
    }
}
