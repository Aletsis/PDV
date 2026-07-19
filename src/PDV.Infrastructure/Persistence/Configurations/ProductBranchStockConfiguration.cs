using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PDV.Domain.Entities;

namespace PDV.Infrastructure.Persistence.Configurations;

public class ProductBranchStockConfiguration : IEntityTypeConfiguration<ProductBranchStock>
{
    public void Configure(EntityTypeBuilder<ProductBranchStock> entity)
    {
        entity.HasKey(e => e.Id);
        
        // Clave única compuesta por ProductId y BranchId
        entity.HasIndex(e => new { e.ProductId, e.BranchId }).IsUnique();

        entity.Property(e => e.Stock)
            .HasPrecision(18, 4)
            .IsRequired();

        entity.Property(e => e.MinStock)
            .HasPrecision(18, 4)
            .IsRequired();

        entity.Property(e => e.RowVersion)
            .IsConcurrencyToken();

        entity.HasOne(e => e.Product)
            .WithMany()
            .HasForeignKey(e => e.ProductId)
            .OnDelete(DeleteBehavior.Cascade);

        entity.HasOne(e => e.Branch)
            .WithMany()
            .HasForeignKey(e => e.BranchId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
