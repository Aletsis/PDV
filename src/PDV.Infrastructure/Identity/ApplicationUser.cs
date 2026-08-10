using Microsoft.AspNetCore.Identity;
using PDV.Domain.Entities;
using System;

namespace PDV.Infrastructure.Identity;

public class ApplicationUser : IdentityUser
{
    public string FullName { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public string? EmployeeNumber { get; set; }
    public Guid? BranchId { get; set; }
    public Branch? Branch { get; set; }
}

