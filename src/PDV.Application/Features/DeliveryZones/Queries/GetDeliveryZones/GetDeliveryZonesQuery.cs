using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PDV.Application.Common.Interfaces;
using PDV.Domain.Entities;

namespace PDV.Application.Features.DeliveryZones.Queries.GetDeliveryZones;

public record GetDeliveryZonesQuery : IRequest<List<DeliveryZone>>
{
    public Guid? BranchId { get; set; }
    public bool OnlyActive { get; set; } = true;
}

public class GetDeliveryZonesQueryHandler : IRequestHandler<GetDeliveryZonesQuery, List<DeliveryZone>>
{
    private readonly IApplicationDbContext _context;

    public GetDeliveryZonesQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<DeliveryZone>> Handle(GetDeliveryZonesQuery request, CancellationToken cancellationToken)
    {
        var query = _context.DeliveryZones.AsQueryable();

        if (request.BranchId.HasValue)
        {
            query = query.Where(z => z.BranchId == request.BranchId.Value);
        }

        if (request.OnlyActive)
        {
            query = query.Where(z => z.IsActive);
        }

        return await query
            .OrderBy(z => z.Name)
            .ToListAsync(cancellationToken);
    }
}
