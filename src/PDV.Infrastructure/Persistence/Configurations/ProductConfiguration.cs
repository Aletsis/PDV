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

        // Índices críticos para búsquedas de catálogo masivo
        entity.HasIndex(e => e.Barcode)
              .HasDatabaseName("IX_Products_Barcode")
              .HasFilter("\"Barcode\" IS NOT NULL");

        entity.HasIndex(e => e.Plu)
              .HasDatabaseName("IX_Products_Plu")
              .HasFilter("\"Plu\" IS NOT NULL");

        entity.HasIndex(e => e.Name)
              .HasDatabaseName("IX_Products_Name_GIN")
              .HasAnnotation("Npgsql:IndexMethod", "gin")
              .HasAnnotation("Npgsql:IndexOperators", new[] { "gin_trgm_ops" });

        entity.Property(e => e.Price).HasPrecision(18, 2);
        entity.Property(e => e.WholesalePrice).HasPrecision(18, 2);
        entity.Property(e => e.WholesaleMinQuantity).HasPrecision(18, 3);
        entity.Property(e => e.Cost).HasPrecision(18, 2);

        entity.Property(e => e.SatCode).HasMaxLength(20);
        entity.Property(e => e.Type).IsRequired();
        entity.Property(e => e.ControlExistencia).IsRequired();
        entity.Property(e => e.SaleUnitId);
        entity.Property(e => e.SaleUnitName).HasMaxLength(50);
        entity.Property(e => e.XmlUnitId);
        entity.Property(e => e.Department).HasMaxLength(100);
        entity.Property(e => e.Clasificacion1Id);
        entity.Property(e => e.Clasificacion5Id);
    }
}
