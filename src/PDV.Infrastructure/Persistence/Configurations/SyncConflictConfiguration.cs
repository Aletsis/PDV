using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PDV.Domain.Entities;

namespace PDV.Infrastructure.Persistence.Configurations;

public class SyncConflictConfiguration : IEntityTypeConfiguration<SyncConflict>
{
    public void Configure(EntityTypeBuilder<SyncConflict> entity)
    {
        entity.Property(e => e.EntityName).IsRequired().HasMaxLength(100);
        entity.Property(e => e.EntityId).IsRequired();
        entity.Property(e => e.ClientValuesJson).IsRequired();
        entity.Property(e => e.ServerValuesJson).IsRequired();
        entity.Property(e => e.ConflictType).IsRequired().HasMaxLength(100);
        entity.Property(e => e.ResolutionStrategy).HasMaxLength(200);

        entity.HasIndex(e => new { e.EntityName, e.EntityId });
        entity.HasIndex(e => e.Resolved);
    }
}
