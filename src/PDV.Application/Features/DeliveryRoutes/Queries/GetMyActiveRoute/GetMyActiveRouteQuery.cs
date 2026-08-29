using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PDV.Application.Common.Interfaces;
using PDV.Domain.Entities;
using PDV.Domain.Enums;

namespace PDV.Application.Features.DeliveryRoutes.Queries.GetMyActiveRoute;

public record GetMyActiveRouteQuery(string DeliveryManId) : IRequest<DeliveryRoute?>;

public class GetMyActiveRouteQueryHandler : IRequestHandler<GetMyActiveRouteQuery, DeliveryRoute?>
{
    private readonly IApplicationDbContext _context;

    public GetMyActiveRouteQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<DeliveryRoute?> Handle(GetMyActiveRouteQuery request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.DeliveryManId))
            return null;

        return await _context.DeliveryRoutes
            .Include(r => r.DeliveryZone)
            .Include(r => r.Orders)
                .ThenInclude(o => o.Client)
            .Include(r => r.Orders)
                .ThenInclude(o => o.Items)
                    .ThenInclude(i => i.Product)
            .Where(r => r.DeliveryManId == request.DeliveryManId &&
                        (r.Status == DeliveryRouteStatus.EnRoute || r.Status == DeliveryRouteStatus.Created))
            .OrderByDescending(r => r.DispatchedDate ?? r.CreatedDate)
            .FirstOrDefaultAsync(cancellationToken);
    }
}
