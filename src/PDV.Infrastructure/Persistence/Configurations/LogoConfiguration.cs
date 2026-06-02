using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PDV.Domain.Entities;

namespace PDV.Infrastructure.Persistence.Configurations;

public class LogoConfiguration : IEntityTypeConfiguration<Logo>
{
    public void Configure(EntityTypeBuilder<Logo> entity)
    {
        entity.Property(e => e.FileName).HasMaxLength(200);
        entity.Property(e => e.ContentType).HasMaxLength(100);
    }
}
