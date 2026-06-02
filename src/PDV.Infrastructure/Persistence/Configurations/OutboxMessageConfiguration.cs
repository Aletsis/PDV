using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PDV.Domain.Entities;

namespace PDV.Infrastructure.Persistence.Configurations;

public class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> entity)
    {
        entity.Property(e => e.EventType).IsRequired().HasMaxLength(150);
        entity.Property(e => e.Payload).IsRequired();
        entity.Property(e => e.State).HasConversion<int>();
        entity.HasIndex(e => new { e.State, e.CreatedAt });
    }
}
