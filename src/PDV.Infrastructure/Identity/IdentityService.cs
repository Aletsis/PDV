using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PDV.Application.Common.Interfaces;
using PDV.Infrastructure.Identity;

namespace PDV.Infrastructure.Identity;

public class IdentityService : IIdentityService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<IdentityRole> _roleManager;

    public IdentityService(UserManager<ApplicationUser> userManager, RoleManager<IdentityRole> roleManager)
    {
        _userManager = userManager;
        _roleManager = roleManager;
    }

    public async Task<List<UserSyncDataDto>> GetUsersDeltaAsync(DateTime sinceUtc, CancellationToken cancellationToken)
    {
        // Al consultar usuarios, los obtenemos de Identity
        var users = await _userManager.Users.AsNoTracking().ToListAsync(cancellationToken);
        var result = new List<UserSyncDataDto>();

        foreach (var u in users)
        {
            var roles = await _userManager.GetRolesAsync(u);
            result.Add(new UserSyncDataDto
            {
                Id = u.Id,
                UserName = u.UserName ?? string.Empty,
                Email = u.Email,
                FullName = u.FullName,
                IsActive = u.IsActive,
                PasswordHash = u.PasswordHash,
                Roles = roles.ToList()
            });
        }

        return result;
    }
}
