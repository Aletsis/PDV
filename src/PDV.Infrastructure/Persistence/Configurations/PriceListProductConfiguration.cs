using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PDV.Domain.Entities;

namespace PDV.Infrastructure.Persistence.Configurations;

public class PriceListProductConfiguration : IEntityTypeConfiguration<PriceListProduct>
{
    public void Configure(EntityTypeBuilder<PriceListProduct> builder)
    {
        builder.HasKey(pp => new { pp.PriceListId, pp.ProductId });
        builder.Property(pp => pp.Price).HasPrecision(18, 2);

        builder.HasOne(pp => pp.Product)
            .WithMany(p => p.PriceListProducts)
            .HasForeignKey(pp => pp.ProductId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
