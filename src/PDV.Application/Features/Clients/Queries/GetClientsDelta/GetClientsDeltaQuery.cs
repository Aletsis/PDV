using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PDV.Application.Common.Interfaces;

namespace PDV.Application.Features.Clients.Queries.GetClientsDelta;

public record GetClientsDeltaQuery(DateTime SinceUtc) : IRequest<List<ClientSyncDto>>;

public class ClientSyncDto
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string TaxId { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Street { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? ZipCode { get; set; }
    public string? Country { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? LastModifiedAt { get; set; }
}

public class GetClientsDeltaQueryHandler : IRequestHandler<GetClientsDeltaQuery, List<ClientSyncDto>>
{
    private readonly IApplicationDbContext _context;

    public GetClientsDeltaQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<ClientSyncDto>> Handle(GetClientsDeltaQuery request, CancellationToken cancellationToken)
    {
        var since = request.SinceUtc;

        return await _context.Clients
            .IgnoreQueryFilters() // In case soft-deleted or inactive items need to be sync'd too
            .Where(c => c.CreatedAt > since || (c.LastModifiedAt != null && c.LastModifiedAt > since))
            .Select(c => new ClientSyncDto
            {
                Id = c.Id,
                Code = c.Code,
                Name = c.Name,
                TaxId = c.TaxId,
                Phone = c.Phone,
                Email = c.Email,
                Street = c.Address != null ? c.Address.Street : null,
                City = c.Address != null ? c.Address.City : null,
                State = c.Address != null ? c.Address.State : null,
                ZipCode = c.Address != null ? c.Address.ZipCode : null,
                Country = c.Address != null ? c.Address.Country : null,
                IsActive = c.IsActive,
                CreatedAt = c.CreatedAt,
                LastModifiedAt = c.LastModifiedAt
            })
            .ToListAsync(cancellationToken);
    }
}
