using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PDV.Domain.Entities;

namespace PDV.Infrastructure.Persistence.Configurations;

public class InvoiceConfiguration : IEntityTypeConfiguration<Invoice>
{
    public void Configure(EntityTypeBuilder<Invoice> entity)
    {
        entity.Property(e => e.Subtotal).HasPrecision(18, 2);
        entity.Property(e => e.TotalTax).HasPrecision(18, 2);
        entity.Property(e => e.Total).HasPrecision(18, 2);

        entity.Property(e => e.SelloDigitalEmisor);
        entity.Property(e => e.SelloDigitalSAT);
        entity.Property(e => e.NoCertificadoEmisor).HasMaxLength(20);
        entity.Property(e => e.NoCertificadoSAT).HasMaxLength(20);
        entity.Property(e => e.CadenaOriginal);

        entity.OwnsMany(e => e.TaxBreakdowns, a =>
        {
            a.ToTable("InvoiceTaxBreakdowns");
            a.WithOwner().HasForeignKey("InvoiceId");
            a.Property(x => x.BaseAmount).HasPrecision(18, 2);
            a.Property(x => x.TaxAmount).HasPrecision(18, 2);
            a.Property(x => x.Rate).HasPrecision(18, 4);
        });
    }
}
