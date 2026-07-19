using MediatR;
using PDV.Application.Common.Interfaces;
using PDV.Application.Common.Security;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace PDV.Application.Common.Behaviors;

public class AuthorizationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly IPermissionService _permissionService;

    public AuthorizationBehavior(
        ICurrentUserService currentUserService,
        IPermissionService permissionService)
    {
        _currentUserService = currentUserService;
        _permissionService = permissionService;
    }

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        var authorizeAttributes = request.GetType().GetCustomAttributes<AuthorizeCommandAttribute>();

        if (authorizeAttributes.Any())
        {
            // Validar que el usuario actual esté autenticado
            if (!_currentUserService.IsAuthenticated)
            {
                throw new UnauthorizedAccessException("Usuario no autenticado.");
            }

            foreach (var attribute in authorizeAttributes)
            {
                var permission = attribute.Permission;
                
                // Obtener los roles del usuario actual
                var roles = _currentUserService.Roles;

                // 1. Verificar si el usuario actual tiene el permiso directamente
                var hasPermission = await _permissionService.HasPermissionAsync(roles, permission, cancellationToken);
                if (hasPermission)
                {
                    continue; // Tiene el permiso directamente, continuar
                }

                // 2. Si no lo tiene directamente, ver si el comando tiene campos de autorización de supervisor
                if (request is ISupervisorAuthorizedCommand supervisorCommand)
                {
                    if (!string.IsNullOrEmpty(supervisorCommand.SupervisorUsername) && 
                        !string.IsNullOrEmpty(supervisorCommand.SupervisorPassword))
                    {
                        var (success, supervisorId, error) = await _permissionService.ValidateSupervisorPermissionAsync(
                            supervisorCommand.SupervisorUsername, 
                            supervisorCommand.SupervisorPassword, 
                            permission, 
                            cancellationToken);

                        if (success)
                        {
                            // Registrar quién autorizó la operación en el comando
                            if (request is ISupervisorAuthorizedTarget target)
                            {
                                target.AuthorizedByUserId = supervisorId;
                            }
                            continue; // Autorización de supervisor válida
                        }
                        else
                        {
                            throw new UnauthorizedAccessException($"Autorización de supervisor fallida: {error}");
                        }
                    }
                }

                // Si no tiene el permiso ni una autorización de supervisor válida, lanzar excepción
                throw new UnauthorizedAccessException($"Acción no autorizada. Se requiere el permiso '{permission}'.");
            }
        }

        return await next();
    }
}
