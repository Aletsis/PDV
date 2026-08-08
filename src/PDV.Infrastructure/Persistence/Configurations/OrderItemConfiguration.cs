using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PDV.Domain.Entities;

namespace PDV.Infrastructure.Persistence.Configurations;

public class OrderItemConfiguration : IEntityTypeConfiguration<OrderItem>
{
    public void Configure(EntityTypeBuilder<OrderItem> entity)
    {
        entity.Property(e => e.Quantity).HasPrecision(18, 4);
        entity.Property(e => e.UnitPrice).HasPrecision(18, 2);
        entity.Property(e => e.TaxRate).HasPrecision(18, 4);

        entity.Property(e => e.ProductName)
              .IsRequired()
              .HasMaxLength(150);

        entity.HasOne(e => e.Product)
              .WithMany()
              .HasForeignKey(e => e.ProductId)
              .OnDelete(DeleteBehavior.Restrict);
    }
}
