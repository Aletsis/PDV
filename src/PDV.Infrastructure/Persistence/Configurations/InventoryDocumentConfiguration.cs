using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PDV.Domain.Entities;

namespace PDV.Infrastructure.Persistence.Configurations;

public class InventoryDocumentConfiguration : IEntityTypeConfiguration<InventoryDocument>
{
    public void Configure(EntityTypeBuilder<InventoryDocument> entity)
    {
        entity.HasKey(e => e.Id);
        entity.Property(e => e.Type).HasConversion<int>();
        entity.Property(e => e.Subtype).HasConversion<int>();
        entity.Property(e => e.SyncStatus).HasConversion<int>();

        entity.Property(e => e.Series).HasMaxLength(30);
        entity.Property(e => e.Folio).HasMaxLength(30);
        entity.Property(e => e.SupplierCode).HasMaxLength(30);
        entity.Property(e => e.SupplierName).HasMaxLength(150);
        entity.Property(e => e.Reference).HasMaxLength(100);
        entity.Property(e => e.Remarks).HasMaxLength(500);
        entity.Property(e => e.CreatedBy).IsRequired().HasMaxLength(100);

        entity.Property(e => e.ExternalSeries).HasMaxLength(30);
        entity.Property(e => e.ExternalFolio).HasMaxLength(30);
        entity.Property(e => e.SyncErrorMessage).HasMaxLength(2000);

        entity.HasOne(d => d.Branch)
              .WithMany()
              .HasForeignKey(d => d.BranchId)
              .OnDelete(DeleteBehavior.Restrict);

        entity.HasOne(d => d.DestinationBranch)
              .WithMany()
              .HasForeignKey(d => d.DestinationBranchId)
              .OnDelete(DeleteBehavior.Restrict);

        entity.HasOne(d => d.Supplier)
              .WithMany()
              .HasForeignKey(d => d.SupplierId)
              .OnDelete(DeleteBehavior.SetNull);

        entity.HasMany(d => d.Items)
              .WithOne(i => i.InventoryDocument)
              .HasForeignKey(i => i.InventoryDocumentId)
              .OnDelete(DeleteBehavior.Cascade);
    }
}
