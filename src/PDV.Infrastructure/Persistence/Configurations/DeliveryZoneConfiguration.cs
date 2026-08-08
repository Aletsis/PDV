using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PDV.Domain.Entities;

namespace PDV.Infrastructure.Persistence.Configurations;

public class DeliveryZoneConfiguration : IEntityTypeConfiguration<DeliveryZone>
{
    public void Configure(EntityTypeBuilder<DeliveryZone> entity)
    {
        entity.Property(e => e.Name)
              .IsRequired()
              .HasMaxLength(100);

        entity.Property(e => e.PolygonCoordinatesJson)
              .IsRequired();

        entity.Property(e => e.DeliveryCost)
              .HasPrecision(18, 2);

        entity.HasOne(e => e.Branch)
              .WithMany()
              .HasForeignKey(e => e.BranchId)
              .OnDelete(DeleteBehavior.Restrict);
    }
}
