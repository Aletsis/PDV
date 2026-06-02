using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PDV.Domain.Entities;

namespace PDV.Infrastructure.Persistence.Configurations;

public class SystemConfigurationConfiguration : IEntityTypeConfiguration<SystemConfiguration>
{
    public void Configure(EntityTypeBuilder<SystemConfiguration> builder)
    {
        builder.ToTable("SystemConfiguration");
        
        builder.HasKey(c => c.Id);
        
        builder.Property(c => c.CompanyName)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(c => c.TaxId)
            .HasMaxLength(50);
            
        builder.OwnsOne(c => c.FiscalAddress, a =>
        {
            a.Property(ad => ad.Street).HasMaxLength(200);
            a.Property(ad => ad.City).HasMaxLength(100);
            a.Property(ad => ad.State).HasMaxLength(100);
            a.Property(ad => ad.ZipCode).HasMaxLength(20);
            a.Property(ad => ad.Country).HasMaxLength(100);
        });

        builder.Property(c => c.Phone)
            .HasMaxLength(20);

        builder.Property(c => c.Email)
            .HasMaxLength(100);
            
        builder.Property(c => c.Currency)
            .HasMaxLength(10);

        builder.Property(c => c.TicketHeader)
            .HasMaxLength(500);

        builder.Property(c => c.SmtpServer)
            .HasMaxLength(200);

        builder.Property(c => c.SmtpUser)
            .HasMaxLength(150);

        builder.Property(c => c.SmtpPassword)
            .HasMaxLength(200);

        builder.Property(c => c.BackupDirectory)
            .HasMaxLength(500);

        builder.Property(c => c.AutoReportUsers)
            .HasMaxLength(1000);

        builder.Property(c => c.ComercialApiUrl)
            .HasMaxLength(500);

        builder.Property(c => c.ComercialApiKey)
            .HasMaxLength(500);
    }
}
