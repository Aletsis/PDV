using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PDV.Domain.Entities;

namespace PDV.Infrastructure.Persistence.Configurations;

public class CashRegisterConfiguration : IEntityTypeConfiguration<CashRegister>
{
    public void Configure(EntityTypeBuilder<CashRegister> entity)
    {
        entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
        entity.Property(e => e.Location).IsRequired().HasMaxLength(200);

        // IpAddress: nullable, máx 45 chars (IPv4 = 15, IPv6 = 39, con prefijo = 45)
        entity.Property(e => e.IpAddress)
              .IsRequired(false)
              .HasMaxLength(45);

        // Índice único sobre IpAddress: garantiza unicidad a nivel de BD.
        // Las filas con NULL no participan en el índice (comportamiento estándar SQL).
        entity.HasIndex(e => e.IpAddress)
              .IsUnique()
              .HasFilter("\"IpAddress\" IS NOT NULL");

        entity.HasOne(e => e.Branch)
              .WithMany()
              .HasForeignKey(e => e.BranchId)
              .OnDelete(DeleteBehavior.Restrict);

        entity.HasOne(e => e.AssignedPrinter)
              .WithMany()
              .HasForeignKey(e => e.AssignedPrinterId)
              .OnDelete(DeleteBehavior.SetNull);
    }
}

