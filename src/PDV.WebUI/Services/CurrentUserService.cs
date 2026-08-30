using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Http;
using PDV.Application.Common.Interfaces;
using System.Collections.Generic;
using System.Linq;

namespace PDV.WebUI.Services;

/// <summary>
/// Implementación de ICurrentUserService para Blazor usando AuthenticationStateProvider y HttpContextAccessor
/// </summary>
public class CurrentUserService : ICurrentUserService
{
    private readonly AuthenticationStateProvider _authenticationStateProvider;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserService(
        AuthenticationStateProvider authenticationStateProvider,
        IHttpContextAccessor httpContextAccessor)
    {
        _authenticationStateProvider = authenticationStateProvider;
        _httpContextAccessor = httpContextAccessor;
    }

    public string? UserId
    {
        get
        {
            try
            {
                var authState = _authenticationStateProvider.GetAuthenticationStateAsync().Result;
                return authState.User?.Identity?.IsAuthenticated == true
                    ? authState.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                    : null;
            }
            catch (System.InvalidOperationException)
            {
                return null;
            }
        }
    }

    public string? UserName
    {
        get
        {
            try
            {
                var authState = _authenticationStateProvider.GetAuthenticationStateAsync().Result;
                return authState.User?.Identity?.IsAuthenticated == true
                    ? authState.User?.Identity?.Name
                    : null;
            }
            catch (System.InvalidOperationException)
            {
                return null;
            }
        }
    }

    public bool IsAuthenticated
    {
        get
        {
            try
            {
                var authState = _authenticationStateProvider.GetAuthenticationStateAsync().Result;
                return authState.User?.Identity?.IsAuthenticated == true;
            }
            catch (System.InvalidOperationException)
            {
                return false;
            }
        }
    }

    public List<string> Roles
    {
        get
        {
            try
            {
                var authState = _authenticationStateProvider.GetAuthenticationStateAsync().Result;
                if (authState.User?.Identity?.IsAuthenticated == true)
                {
                    return authState.User.FindAll(System.Security.Claims.ClaimTypes.Role)
                        .Select(c => c.Value)
                        .ToList();
                }
                return new List<string>();
            }
            catch
            {
                return new List<string>();
            }
        }
    }

    public string? IpAddress
    {
        get
        {
            try
            {
                var context = _httpContextAccessor.HttpContext;
                return context?.Connection?.RemoteIpAddress?.ToString() ?? "127.0.0.1";
            }
            catch
            {
                return "127.0.0.1";
            }
        }
    }

    public System.Guid? BranchId
    {
        get
        {
            try
            {
                var authState = _authenticationStateProvider.GetAuthenticationStateAsync().Result;
                var branchVal = authState.User?.FindFirst("BranchId")?.Value;
                if (System.Guid.TryParse(branchVal, out var bId))
                {
                    return bId;
                }
                return null;
            }
            catch
            {
                return null;
            }
        }
    }
}
