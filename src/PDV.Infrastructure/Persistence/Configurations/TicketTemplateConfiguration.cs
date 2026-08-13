using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PDV.Domain.Entities;

namespace PDV.Infrastructure.Persistence.Configurations;

public class TicketTemplateConfiguration : IEntityTypeConfiguration<TicketTemplate>
{
    public void Configure(EntityTypeBuilder<TicketTemplate> entity)
    {
        entity.Property(e => e.Name)
            .IsRequired()
            .HasMaxLength(100);

        entity.Property(e => e.TemplateType)
            .IsRequired();

        entity.Property(e => e.ContentJson)
            .IsRequired();

        entity.Property(e => e.IsDefault)
            .IsRequired();
    }
}
