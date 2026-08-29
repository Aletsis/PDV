using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PDV.Domain.Entities;

namespace PDV.Infrastructure.Persistence.Configurations;

public class UserWorkStatusConfiguration : IEntityTypeConfiguration<UserWorkStatus>
{
    public void Configure(EntityTypeBuilder<UserWorkStatus> entity)
    {
        entity.Property(e => e.UserId).IsRequired().HasMaxLength(128);
        entity.Property(e => e.Status).IsRequired();
        entity.Property(e => e.StatusNotes).HasMaxLength(500);

        entity.HasOne(e => e.Branch)
              .WithMany()
              .HasForeignKey(e => e.BranchId)
              .OnDelete(DeleteBehavior.Restrict);

        entity.HasIndex(e => e.UserId)
              .IsUnique()
              .HasDatabaseName("IX_UserWorkStatuses_UserId");

        entity.HasIndex(e => new { e.BranchId, e.Status })
              .HasDatabaseName("IX_UserWorkStatuses_BranchId_Status");
    }
}
