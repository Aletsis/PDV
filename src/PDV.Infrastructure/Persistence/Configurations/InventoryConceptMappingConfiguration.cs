using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PDV.Domain.Entities;

namespace PDV.Infrastructure.Persistence.Configurations;

public class InventoryConceptMappingConfiguration : IEntityTypeConfiguration<InventoryConceptMapping>
{
    public void Configure(EntityTypeBuilder<InventoryConceptMapping> entity)
    {
        entity.HasKey(e => e.Id);
        entity.Property(e => e.Subtype).HasConversion<int>();
        entity.HasIndex(e => e.Subtype).IsUnique();

        entity.Property(e => e.ConceptCode).IsRequired().HasMaxLength(30);
        entity.Property(e => e.ConceptName).IsRequired().HasMaxLength(100);
        entity.Property(e => e.DefaultSeries).HasMaxLength(10);
    }
}
