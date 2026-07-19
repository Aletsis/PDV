using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PDV.Application.Common.Interfaces;
using PDV.Infrastructure.Identity;
using PDV.Infrastructure.Persistence;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace PDV.Infrastructure.Common;

public class PermissionService : IPermissionService
{
    private readonly AppDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;

    public PermissionService(AppDbContext context, UserManager<ApplicationUser> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    public async Task<bool> HasPermissionAsync(List<string> roles, string permissionCode, CancellationToken cancellationToken)
    {
        if (roles == null || roles.Count == 0) return false;

        // Obtener los IDs de los roles correspondientes a los nombres de roles
        var roleIds = await _context.Roles
            .Where(r => roles.Contains(r.Name!))
            .Select(r => r.Id)
            .ToListAsync(cancellationToken);

        if (roleIds.Count == 0) return false;

        // Si es Admin, tiene todos los permisos automáticamente
        var adminRole = await _context.Roles.FirstOrDefaultAsync(r => r.Name == "Admin", cancellationToken);
        if (adminRole != null && roleIds.Contains(adminRole.Id))
        {
            return true;
        }

        // Buscar en la tabla puente RolePermissions
        var hasPerm = await _context.RolePermissions
            .Include(rp => rp.Permission)
            .AnyAsync(rp => roleIds.Contains(rp.RoleId) && rp.Permission.Code == permissionCode.ToLowerInvariant(), cancellationToken);

        return hasPerm;
    }

    public async Task<(bool Success, string? SupervisorUserId, string? ErrorMessage)> ValidateSupervisorPermissionAsync(
        string username, 
        string password, 
        string requiredPermissionCode, 
        CancellationToken cancellationToken)
    {
        // 1. Buscar el usuario supervisor
        var user = await _userManager.FindByNameAsync(username) ?? await _userManager.FindByEmailAsync(username);
        if (user == null || !user.IsActive)
        {
            return (false, null, "Usuario de supervisor no encontrado o inactivo.");
        }

        // 2. Validar contraseña
        var isPasswordValid = await _userManager.CheckPasswordAsync(user, password);
        if (!isPasswordValid)
        {
            return (false, null, "Contraseña de supervisor incorrecta.");
        }

        // 3. Obtener roles del supervisor
        var roles = await _userManager.GetRolesAsync(user);

        // 4. Validar permisos del supervisor
        var hasPermission = await HasPermissionAsync(roles.ToList(), requiredPermissionCode, cancellationToken);
        if (!hasPermission)
        {
            return (false, null, "El supervisor no cuenta con el permiso requerido para esta acción.");
        }

        return (true, user.Id, null);
    }
}
