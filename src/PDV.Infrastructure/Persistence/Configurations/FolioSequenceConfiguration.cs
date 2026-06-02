using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PDV.Domain.Entities;

namespace PDV.Infrastructure.Persistence.Configurations;

public class FolioSequenceConfiguration : IEntityTypeConfiguration<FolioSequence>
{
    public void Configure(EntityTypeBuilder<FolioSequence> builder)
    {
        builder.ToTable("FolioSequences");

        builder.HasKey(f => f.Id);

        builder.Property(f => f.BranchId)
            .IsRequired();

        builder.Property(f => f.SeriesType)
            .IsRequired();

        builder.Property(f => f.Series)
            .IsRequired()
            .HasMaxLength(10);

        builder.Property(f => f.ConceptCode)
            .HasMaxLength(50);

        builder.Property(f => f.LastFolio)
            .IsRequired();

        builder.Property(f => f.FolioDigits)
            .IsRequired();

        // Concurrencia optimista mediante RowVersion (Timestamp en SQL Server)
        builder.Property(f => f.RowVersion)
            .IsRowVersion();

        // Unicidad: No pueden existir dos secuencias del mismo tipo para la misma sucursal
        builder.HasIndex(f => new { f.BranchId, f.SeriesType })
            .IsUnique();

        // Relación con Branch
        builder.HasOne(f => f.Branch)
            .WithMany()
            .HasForeignKey(f => f.BranchId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
