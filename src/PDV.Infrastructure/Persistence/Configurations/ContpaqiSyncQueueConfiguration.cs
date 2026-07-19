using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PDV.Domain.Entities;

namespace PDV.Infrastructure.Persistence.Configurations;

public class ContpaqiSyncQueueConfiguration : IEntityTypeConfiguration<ContpaqiSyncQueue>
{
    public void Configure(EntityTypeBuilder<ContpaqiSyncQueue> entity)
    {
        entity.Property(e => e.ReferenceId).IsRequired();
        entity.Property(e => e.Type).IsRequired().HasMaxLength(50);
        entity.Property(e => e.Action).IsRequired().HasMaxLength(50);
        entity.Property(e => e.State).HasConversion<int>();
        entity.HasIndex(e => new { e.State, e.CreatedAt });
    }
}
