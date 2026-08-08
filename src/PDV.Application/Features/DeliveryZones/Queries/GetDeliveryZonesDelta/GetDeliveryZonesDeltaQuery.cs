using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PDV.Application.Common.Interfaces;

namespace PDV.Application.Features.DeliveryZones.Queries.GetDeliveryZonesDelta;

public record GetDeliveryZonesDeltaQuery(DateTime SinceUtc) : IRequest<List<DeliveryZoneSyncDto>>;

public class DeliveryZoneSyncDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public Guid BranchId { get; set; }
    public string PolygonCoordinatesJson { get; set; } = string.Empty;
    public decimal DeliveryCost { get; set; }
    public bool IsActive { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? LastModifiedAt { get; set; }
}

public class GetDeliveryZonesDeltaQueryHandler : IRequestHandler<GetDeliveryZonesDeltaQuery, List<DeliveryZoneSyncDto>>
{
    private readonly IApplicationDbContext _context;

    public GetDeliveryZonesDeltaQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<DeliveryZoneSyncDto>> Handle(GetDeliveryZonesDeltaQuery request, CancellationToken cancellationToken)
    {
        var since = request.SinceUtc;

        return await _context.DeliveryZones
            .IgnoreQueryFilters()
            .Where(z => z.CreatedAt > since || (z.LastModifiedAt != null && z.LastModifiedAt > since))
            .Select(z => new DeliveryZoneSyncDto
            {
                Id = z.Id,
                Name = z.Name,
                BranchId = z.BranchId,
                PolygonCoordinatesJson = z.PolygonCoordinatesJson,
                DeliveryCost = z.DeliveryCost,
                IsActive = z.IsActive,
                IsDeleted = z.IsDeleted,
                CreatedAt = z.CreatedAt,
                LastModifiedAt = z.LastModifiedAt
            })
            .ToListAsync(cancellationToken);
    }
}
