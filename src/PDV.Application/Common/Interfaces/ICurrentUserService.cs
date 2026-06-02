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
}
