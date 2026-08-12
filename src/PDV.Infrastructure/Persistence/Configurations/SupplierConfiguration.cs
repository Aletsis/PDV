using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PDV.Domain.Entities;

namespace PDV.Infrastructure.Persistence.Configurations;

public class SupplierConfiguration : IEntityTypeConfiguration<Supplier>
{
    public void Configure(EntityTypeBuilder<Supplier> entity)
    {
        entity.HasKey(e => e.Id);
        entity.Property(e => e.Code).IsRequired().HasMaxLength(30);
        entity.HasIndex(e => e.Code).IsUnique();

        entity.Property(e => e.Name).IsRequired().HasMaxLength(150);
        entity.Property(e => e.TaxId).HasMaxLength(20);
        entity.Property(e => e.Phone).HasMaxLength(30);
        entity.Property(e => e.Email).HasMaxLength(100);

        entity.OwnsOne(e => e.Address, a =>
        {
            a.Property(x => x.Street).HasMaxLength(150).HasColumnName("Street");
            a.Property(x => x.ExteriorNumber).HasMaxLength(50).HasColumnName("ExteriorNumber");
            a.Property(x => x.InteriorNumber).HasMaxLength(50).HasColumnName("InteriorNumber");
            a.Property(x => x.Colony).HasMaxLength(150).HasColumnName("Colony");
            a.Property(x => x.City).HasMaxLength(100).HasColumnName("City");
            a.Property(x => x.State).HasMaxLength(100).HasColumnName("State");
            a.Property(x => x.ZipCode).HasMaxLength(20).HasColumnName("ZipCode");
            a.Property(x => x.Country).HasMaxLength(100).HasColumnName("Country");
        });
    }
}
