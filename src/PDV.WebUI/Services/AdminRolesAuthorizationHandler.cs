using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using System.Threading.Tasks;

namespace PDV.WebUI.Services;

/// <summary>
/// Interceptor global de autorización que asegura que los usuarios con el rol "Admin"
/// tengan acceso automático a cualquier recurso restringido por roles (ej. requisitos de Cashier o Manager).
/// </summary>
public class AdminRolesAuthorizationHandler : AuthorizationHandler<RolesAuthorizationRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        RolesAuthorizationRequirement requirement)
    {
        if (context.User.IsInRole("Admin"))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
