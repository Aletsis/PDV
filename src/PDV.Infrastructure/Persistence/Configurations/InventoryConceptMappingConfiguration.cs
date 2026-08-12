using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PDV.Domain.Entities;

namespace PDV.Infrastructure.Persistence.Configurations;

public class InventoryConceptMappingConfiguration : IEntityTypeConfiguration<InventoryConceptMapping>
{
    public void Configure(EntityTypeBuilder<InventoryConceptMapping> entity)
    {
        entity.ToTable("InventoryConceptMappings");

        entity.HasKey(e => e.Id);

        entity.Property(e => e.BranchId)
            .IsRequired();

        entity.Property(e => e.MovementType)
            .IsRequired()
            .HasConversion<int>();

        entity.Property(e => e.Subtype)
            .HasConversion<int?>();

        entity.Property(e => e.DestinationBranchId);

        entity.Property(e => e.ConceptCode)
            .IsRequired()
            .HasMaxLength(50);

        entity.Property(e => e.ConceptName)
            .IsRequired()
            .HasMaxLength(100);

        entity.Property(e => e.DefaultSeries)
            .HasMaxLength(10);

        // Relaciones con Branch
        entity.HasOne(e => e.Branch)
            .WithMany()
            .HasForeignKey(e => e.BranchId)
            .OnDelete(DeleteBehavior.Restrict);

        entity.HasOne(e => e.DestinationBranch)
            .WithMany()
            .HasForeignKey(e => e.DestinationBranchId)
            .OnDelete(DeleteBehavior.Restrict);

        // Índices para búsquedas rápidas y evitar duplicados por sucursal
        entity.HasIndex(e => new { e.BranchId, e.MovementType, e.Subtype });
        entity.HasIndex(e => new { e.BranchId, e.DestinationBranchId, e.Subtype });
    }
}
