using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PDV.Domain.Entities;

namespace PDV.Infrastructure.Persistence.Configurations;

public class PriceListConfiguration : IEntityTypeConfiguration<PriceList>
{
    public void Configure(EntityTypeBuilder<PriceList> builder)
    {
        builder.HasKey(pl => pl.Id);
        builder.Property(pl => pl.Name).IsRequired().HasMaxLength(100);
        builder.Property(pl => pl.Description).HasMaxLength(250);
        
        builder.HasMany(pl => pl.ProductPrices)
            .WithOne()
            .HasForeignKey(pp => pp.PriceListId)
            .OnDelete(DeleteBehavior.Cascade);
            
        builder.Navigation(pl => pl.ProductPrices)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
