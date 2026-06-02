using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PDV.Application.Common.Interfaces;

namespace PDV.Application.Features.Branches.Queries.GetBranchesDelta;

public record GetBranchesDeltaQuery(DateTime SinceUtc) : IRequest<List<BranchSyncDto>>;

public class BranchSyncDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string? Street { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? ZipCode { get; set; }
    public string? Country { get; set; }
    public string Phone { get; set; } = string.Empty;
    public string? Email { get; set; }
    public bool IsActive { get; set; }
    public bool IsMainBranch { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? LastModifiedAt { get; set; }
}

public class GetBranchesDeltaQueryHandler : IRequestHandler<GetBranchesDeltaQuery, List<BranchSyncDto>>
{
    private readonly IApplicationDbContext _context;

    public GetBranchesDeltaQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<BranchSyncDto>> Handle(GetBranchesDeltaQuery request, CancellationToken cancellationToken)
    {
        var since = request.SinceUtc;

        return await _context.Branches
            .IgnoreQueryFilters() // In case soft-deleted or inactive items need to be sync'd too
            .Where(b => b.CreatedAt > since || (b.LastModifiedAt != null && b.LastModifiedAt > since))
            .Select(b => new BranchSyncDto
            {
                Id = b.Id,
                Name = b.Name,
                Code = b.Code,
                Street = b.Address != null ? b.Address.Street : null,
                City = b.Address != null ? b.Address.City : null,
                State = b.Address != null ? b.Address.State : null,
                ZipCode = b.Address != null ? b.Address.ZipCode : null,
                Country = b.Address != null ? b.Address.Country : null,
                Phone = b.Phone,
                Email = b.Email,
                IsActive = b.IsActive,
                IsMainBranch = b.IsMainBranch,
                CreatedAt = b.CreatedAt,
                LastModifiedAt = b.LastModifiedAt
            })
            .ToListAsync(cancellationToken);
    }
}
