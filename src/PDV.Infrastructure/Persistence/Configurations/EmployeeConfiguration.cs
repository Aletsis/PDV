using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PDV.Domain.Entities;

namespace PDV.Infrastructure.Persistence.Configurations;

public class EmployeeConfiguration : IEntityTypeConfiguration<Employee>
{
    public void Configure(EntityTypeBuilder<Employee> entity)
    {
        entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
        entity.Property(e => e.EmployeeCode).IsRequired().HasMaxLength(50);
        entity.HasIndex(e => e.EmployeeCode).IsUnique();
    }
}
