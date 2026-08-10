using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PDV.Infrastructure.Identity;

namespace PDV.Infrastructure.Persistence.Configurations;

public class ApplicationUserConfiguration : IEntityTypeConfiguration<ApplicationUser>
{
    public void Configure(EntityTypeBuilder<ApplicationUser> entity)
    {
        entity.Property(e => e.EmployeeNumber)
              .IsRequired(false)
              .HasMaxLength(50);

        entity.HasOne(e => e.Branch)
              .WithMany()
              .HasForeignKey(e => e.BranchId)
              .OnDelete(DeleteBehavior.SetNull);
    }
}
