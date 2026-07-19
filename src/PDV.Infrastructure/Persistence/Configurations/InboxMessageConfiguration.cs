using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PDV.Domain.Entities;

namespace PDV.Infrastructure.Persistence.Configurations;

public class InboxMessageConfiguration : IEntityTypeConfiguration<InboxMessage>
{
    public void Configure(EntityTypeBuilder<InboxMessage> entity)
    {
        entity.Property(e => e.MessageId).IsRequired();
        entity.HasIndex(e => e.MessageId).IsUnique();

        entity.Property(e => e.EventType).IsRequired().HasMaxLength(150);
        entity.Property(e => e.Payload).IsRequired();
        entity.Property(e => e.State).HasConversion<int>();
        
        entity.HasIndex(e => new { e.State, e.ReceivedAt });
    }
}
