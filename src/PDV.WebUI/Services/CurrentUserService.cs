using Microsoft.AspNetCore.Components.Authorization;
using PDV.Application.Common.Interfaces;

namespace PDV.WebUI.Services;

/// <summary>
/// Implementación de ICurrentUserService para Blazor usando AuthenticationStateProvider
/// </summary>
public class CurrentUserService : ICurrentUserService
{
    private readonly AuthenticationStateProvider _authenticationStateProvider;

    public CurrentUserService(AuthenticationStateProvider authenticationStateProvider)
    {
        _authenticationStateProvider = authenticationStateProvider;
    }

    public string? UserId
    {
        get
        {
            var authState = _authenticationStateProvider.GetAuthenticationStateAsync().Result;
            return authState.User?.Identity?.IsAuthenticated == true
                ? authState.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                : null;
        }
    }

    public string? UserName
    {
        get
        {
            var authState = _authenticationStateProvider.GetAuthenticationStateAsync().Result;
            return authState.User?.Identity?.IsAuthenticated == true
                ? authState.User?.Identity?.Name
                : null;
        }
    }

    public bool IsAuthenticated
    {
        get
        {
            var authState = _authenticationStateProvider.GetAuthenticationStateAsync().Result;
            return authState.User?.Identity?.IsAuthenticated == true;
        }
    }
}
