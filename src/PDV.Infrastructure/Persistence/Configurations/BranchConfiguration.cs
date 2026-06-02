using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PDV.Domain.Entities;

namespace PDV.Infrastructure.Persistence.Configurations;

public class BranchConfiguration : IEntityTypeConfiguration<Branch>
{
    public void Configure(EntityTypeBuilder<Branch> builder)
    {
        builder.ToTable("Branches");
        
        builder.HasKey(b => b.Id);
        
        builder.Property(b => b.Name)
            .IsRequired()
            .HasMaxLength(100);
        
        builder.Property(b => b.Code)
            .IsRequired()
            .HasMaxLength(20);
        
        builder.HasIndex(b => b.Code)
            .IsUnique();
        
        builder.OwnsOne(b => b.Address, a =>
        {
            a.Property(x => x.Street).HasMaxLength(150).HasColumnName("Street");
            a.Property(x => x.City).HasMaxLength(100).HasColumnName("City");
            a.Property(x => x.State).HasMaxLength(100).HasColumnName("State");
            a.Property(x => x.ZipCode).HasMaxLength(20).HasColumnName("ZipCode");
            a.Property(x => x.Country).HasMaxLength(100).HasColumnName("Country");
        });
        
        builder.Property(b => b.Phone)
            .HasMaxLength(20);
        
        builder.Property(b => b.Email)
            .HasMaxLength(100);

        builder.Property(b => b.IsActive)
            .IsRequired();

        builder.Property(b => b.IsMainBranch)
            .IsRequired();
    }
}
