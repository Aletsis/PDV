using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PDV.Domain.Entities;

namespace PDV.Infrastructure.Persistence.Configurations;

public class TicketSequenceConfiguration : IEntityTypeConfiguration<TicketSequence>
{
    public void Configure(EntityTypeBuilder<TicketSequence> builder)
    {
        builder.ToTable("TicketSequences");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.CashRegisterId)
            .IsRequired();

        builder.Property(t => t.SequenceType)
            .IsRequired();

        builder.Property(t => t.LastTicketNumber)
            .IsRequired();

        builder.Property(t => t.ResetOnNewShift)
            .IsRequired();

        builder.Property(t => t.Series)
            .HasMaxLength(10);

        // Concurrencia optimista mediante RowVersion
        builder.Property(t => t.RowVersion)
            .IsRowVersion();

        // Unicidad: No pueden existir dos secuencias del mismo tipo para la misma caja
        builder.HasIndex(t => new { t.CashRegisterId, t.SequenceType })
            .IsUnique();

        // Relación con CashRegister
        builder.HasOne(t => t.CashRegister)
            .WithMany()
            .HasForeignKey(t => t.CashRegisterId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
