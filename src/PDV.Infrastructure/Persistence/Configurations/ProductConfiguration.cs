using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PDV.Domain.Entities;

namespace PDV.Infrastructure.Persistence.Configurations;

public class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> entity)
    {
        entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
        entity.Property(e => e.Code).IsRequired().HasMaxLength(50);
        entity.HasIndex(e => e.Code).IsUnique();

        entity.Property(e => e.Price).HasPrecision(18, 2);
        entity.Property(e => e.WholesalePrice).HasPrecision(18, 2);
        entity.Property(e => e.WholesaleMinQuantity).HasPrecision(18, 3);
        entity.Property(e => e.Cost).HasPrecision(18, 2);
        entity.Property(e => e.Stock).HasPrecision(18, 4);
        entity.Property(e => e.MinStock).HasPrecision(18, 4);

        entity.Property(e => e.RowVersion).IsConcurrencyToken();

        entity.Property(e => e.SatCode).HasMaxLength(20);
        entity.Property(e => e.Type).IsRequired();
        entity.Property(e => e.ControlExistencia).IsRequired();
        entity.Property(e => e.SaleUnitId);
        entity.Property(e => e.SaleUnitName).HasMaxLength(50);
        entity.Property(e => e.XmlUnitId);
        entity.Property(e => e.Department).HasMaxLength(100);
        entity.Property(e => e.Clasificacion1Id);
        entity.Property(e => e.Clasificacion5Id);

        entity.HasMany(e => e.Movements)
              .WithOne(m => m.Product)
              .HasForeignKey(m => m.ProductId)
              .OnDelete(DeleteBehavior.Cascade);

        entity.Navigation(e => e.Movements)
              .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
