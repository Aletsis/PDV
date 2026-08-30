using System.Collections.Generic;

namespace PDV.Application.Common.Interfaces;

/// <summary>
/// Servicio para obtener información del usuario actual
/// Abstracción que puede implementarse tanto en Blazor como en WPF
/// </summary>
public interface ICurrentUserService
{
    string? UserId { get; }
    string? UserName { get; }
    bool IsAuthenticated { get; }
    List<string> Roles { get; }
    string? IpAddress { get; }
    System.Guid? BranchId { get; }
}
