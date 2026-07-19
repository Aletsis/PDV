using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace PDV.Application.Common.Interfaces;

public interface IPermissionService
{
    Task<bool> HasPermissionAsync(List<string> roles, string permissionCode, CancellationToken cancellationToken);
    
    Task<(bool Success, string? SupervisorUserId, string? ErrorMessage)> ValidateSupervisorPermissionAsync(
        string username, 
        string password, 
        string requiredPermissionCode, 
        CancellationToken cancellationToken);
}
