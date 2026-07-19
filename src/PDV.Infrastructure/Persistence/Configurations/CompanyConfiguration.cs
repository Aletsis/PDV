using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PDV.Domain.Entities;

namespace PDV.Infrastructure.Persistence.Configurations;

public class CompanyConfiguration : IEntityTypeConfiguration<Company>
{
    public void Configure(EntityTypeBuilder<Company> builder)
    {
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Name).IsRequired().HasMaxLength(150);
        builder.Property(c => c.RFC).IsRequired().HasMaxLength(13);
        builder.HasIndex(c => c.RFC).IsUnique();
        builder.Property(c => c.Phone).HasMaxLength(20);
        builder.Property(c => c.Email).HasMaxLength(100);

        builder.OwnsOne(c => c.FiscalAddress, a =>
        {
            a.Property(ad => ad.Street).HasMaxLength(150);
            a.Property(ad => ad.City).HasMaxLength(100);
            a.Property(ad => ad.State).HasMaxLength(100);
            a.Property(ad => ad.ZipCode).HasMaxLength(10);
            a.Property(ad => ad.Country).HasMaxLength(50);
        });

        builder.HasMany(c => c.Branches)
            .WithOne(b => b.Company)
            .HasForeignKey(b => b.CompanyId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
