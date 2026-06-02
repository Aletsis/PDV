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
        entity.Property(e => e.Remarks).HasMaxLength(255);
    }
}
