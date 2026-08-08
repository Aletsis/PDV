using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PDV.Domain.Entities;

namespace PDV.Infrastructure.Persistence.Configurations;

public class DeliveryRouteConfiguration : IEntityTypeConfiguration<DeliveryRoute>
{
    public void Configure(EntityTypeBuilder<DeliveryRoute> entity)
    {
        entity.Property(e => e.Folio).IsRequired();
        entity.Property(e => e.DeliveryManId).IsRequired().HasMaxLength(50);

        entity.HasOne(e => e.Branch)
              .WithMany()
              .HasForeignKey(e => e.BranchId)
              .OnDelete(DeleteBehavior.Restrict);

        entity.HasOne(e => e.DeliveryZone)
              .WithMany()
              .HasForeignKey(e => e.DeliveryZoneId)
              .OnDelete(DeleteBehavior.SetNull);

        entity.HasIndex(e => new { e.BranchId, e.Folio })
              .IsUnique()
              .HasDatabaseName("IX_DeliveryRoutes_BranchId_Folio");
    }
}
