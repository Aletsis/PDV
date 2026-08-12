using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PDV.Domain.Entities;
using PDV.Domain.Enums;

namespace PDV.Infrastructure.Persistence.Configurations;

public class InventoryMovementConfiguration : IEntityTypeConfiguration<InventoryMovement>
{
    public void Configure(EntityTypeBuilder<InventoryMovement> entity)
    {
        entity.HasKey(e => e.Id);
        entity.Property(e => e.Quantity).HasPrecision(18, 4);
        entity.Property(e => e.Type).HasConversion<int>();
        entity.Property(e => e.Subtype).HasConversion<int>();
        entity.Property(e => e.Remarks).HasMaxLength(255);

        entity.HasOne(m => m.Product)
              .WithMany()
              .HasForeignKey(m => m.ProductId)
              .OnDelete(DeleteBehavior.Cascade);

        entity.HasOne(m => m.Branch)
              .WithMany()
              .HasForeignKey(m => m.BranchId)
              .OnDelete(DeleteBehavior.Restrict);

        entity.HasOne(m => m.Document)
              .WithMany()
              .HasForeignKey(m => m.DocumentId)
              .OnDelete(DeleteBehavior.SetNull);
    }
}
