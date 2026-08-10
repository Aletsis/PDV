using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PDV.Domain.Entities;

namespace PDV.Infrastructure.Persistence.Configurations;

public class ClientConfiguration : IEntityTypeConfiguration<Client>
{
    public void Configure(EntityTypeBuilder<Client> entity)
    {
        entity.Property(e => e.Code).IsRequired().HasMaxLength(30).HasDefaultValue("");
        entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
        entity.Property(e => e.TaxId).IsRequired().HasMaxLength(50);
        entity.Property(e => e.Phone).HasMaxLength(20);
        entity.Property(e => e.Email).HasMaxLength(100);
        entity.Property(e => e.FiscalRegime).HasMaxLength(10);
        entity.Property(e => e.FiscalZipCode).HasMaxLength(10);
        entity.Property(e => e.CfdiUse).HasMaxLength(10);

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

        entity.HasOne(e => e.DeliveryZone)
              .WithMany()
              .HasForeignKey(e => e.DeliveryZoneId)
              .OnDelete(DeleteBehavior.SetNull);

        entity.Property(e => e.Latitude).HasPrecision(9, 6);
        entity.Property(e => e.Longitude).HasPrecision(9, 6);
    }
}
