using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PDV.Domain.Entities;

namespace PDV.Infrastructure.Persistence.Configurations;

public class PrinterConfiguration : IEntityTypeConfiguration<Printer>
{
    public void Configure(EntityTypeBuilder<Printer> entity)
    {
        entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
        entity.Property(e => e.IpAddress).IsRequired().HasMaxLength(100);
        entity.Property(e => e.Port).IsRequired();
        entity.Property(e => e.MaxWidth).IsRequired();
    }
}
