using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PDV.Application.Common.Interfaces;

namespace PDV.Application.Features.Sync.Queries.GetUsersDelta;

public record GetUsersDeltaQuery(DateTime SinceUtc) : IRequest<List<UserSyncDto>>;

public class UserSyncDto
{
    public string Id { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string FullName { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public string? PasswordHash { get; set; }
    public List<string> Roles { get; set; } = new();
}

public class GetUsersDeltaQueryHandler : IRequestHandler<GetUsersDeltaQuery, List<UserSyncDto>>
{
    private readonly IIdentityService _identityService;

    public GetUsersDeltaQueryHandler(IIdentityService identityService)
    {
        _identityService = identityService;
    }

    public async Task<List<UserSyncDto>> Handle(GetUsersDeltaQuery request, CancellationToken cancellationToken)
    {
        var users = await _identityService.GetUsersDeltaAsync(request.SinceUtc, cancellationToken);

        return users.Select(u => new UserSyncDto
        {
            Id = u.Id,
            UserName = u.UserName,
            Email = u.Email,
            FullName = u.FullName,
            IsActive = u.IsActive,
            PasswordHash = u.PasswordHash,
            Roles = u.Roles
        }).ToList();
    }
}
