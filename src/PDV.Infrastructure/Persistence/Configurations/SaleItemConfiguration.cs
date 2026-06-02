using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PDV.Domain.Entities;

namespace PDV.Infrastructure.Persistence.Configurations;

public class SaleItemConfiguration : IEntityTypeConfiguration<SaleItem>
{
    public void Configure(EntityTypeBuilder<SaleItem> entity)
    {
        entity.Property(e => e.UnitPrice).HasPrecision(18, 2);
        entity.Property(e => e.PriceOverride).HasPrecision(18, 2);
    }
}
