using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PDV.Application.Common.Interfaces;
using PDV.Domain.Entities;
using PDV.Domain.Enums;

namespace PDV.Application.Features.DeliveryRoutes.Queries.GetDeliveryRoutes;

public record GetDeliveryRoutesQuery : IRequest<List<DeliveryRoute>>
{
    public Guid? BranchId { get; set; }
    public DeliveryRouteStatus? Status { get; set; }
    public string? DeliveryManId { get; set; }
}

public class GetDeliveryRoutesQueryHandler : IRequestHandler<GetDeliveryRoutesQuery, List<DeliveryRoute>>
{
    private readonly IApplicationDbContext _context;

    public GetDeliveryRoutesQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<DeliveryRoute>> Handle(GetDeliveryRoutesQuery request, CancellationToken cancellationToken)
    {
        var query = _context.DeliveryRoutes
            .Include(r => r.DeliveryZone)
            .Include(r => r.Orders)
            .ThenInclude(o => o.Client)
            .AsQueryable();

        if (request.BranchId.HasValue)
        {
            query = query.Where(r => r.BranchId == request.BranchId.Value);
        }

        if (request.Status.HasValue)
        {
            query = query.Where(r => r.Status == request.Status.Value);
        }

        if (!string.IsNullOrWhiteSpace(request.DeliveryManId))
        {
            query = query.Where(r => r.DeliveryManId == request.DeliveryManId);
        }

        return await query
            .OrderByDescending(r => r.CreatedDate)
            .ToListAsync(cancellationToken);
    }
}
