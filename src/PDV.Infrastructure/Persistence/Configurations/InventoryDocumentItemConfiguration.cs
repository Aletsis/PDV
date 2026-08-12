using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PDV.Domain.Entities;

namespace PDV.Infrastructure.Persistence.Configurations;

public class InventoryDocumentItemConfiguration : IEntityTypeConfiguration<InventoryDocumentItem>
{
    public void Configure(EntityTypeBuilder<InventoryDocumentItem> entity)
    {
        entity.HasKey(e => e.Id);
        entity.Property(e => e.Quantity).HasPrecision(18, 4);
        entity.Property(e => e.UnitCost).HasPrecision(18, 4);
        entity.Property(e => e.Remarks).HasMaxLength(255);

        entity.HasOne(i => i.Product)
              .WithMany()
              .HasForeignKey(i => i.ProductId)
              .OnDelete(DeleteBehavior.Restrict);
    }
}
