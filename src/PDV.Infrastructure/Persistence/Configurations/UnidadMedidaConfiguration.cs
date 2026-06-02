using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PDV.Domain.Entities;

namespace PDV.Infrastructure.Persistence.Configurations;

public class UnidadMedidaConfiguration : IEntityTypeConfiguration<UnidadMedida>
{
    public void Configure(EntityTypeBuilder<UnidadMedida> builder)
    {
        builder.HasKey(u => u.Id);
        
        builder.Property(u => u.ExternalId)
            .IsRequired();

        builder.Property(u => u.NombreUnidad)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(u => u.Abreviatura)
            .HasMaxLength(20);

        builder.Property(u => u.Despliegue)
            .HasMaxLength(50);

        builder.Property(u => u.ClaveInt)
            .HasMaxLength(20);

        builder.Property(u => u.ClaveSat)
            .HasMaxLength(20);
            
        // Crear un índice único por ExternalId para agilizar la sincronización
        builder.HasIndex(u => u.ExternalId)
            .IsUnique();
    }
}
