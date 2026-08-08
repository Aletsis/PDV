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

namespace PDV.Application.Features.Orders.Queries.GetOrders;

public record GetOrdersQuery : IRequest<List<Order>>
{
    public Guid? BranchId { get; set; }
    public OrderStatus? Status { get; set; }
    public Guid? DeliveryRouteId { get; set; }
    public Guid? DeliveryZoneId { get; set; }
    public bool LoadItems { get; set; } = false;
}

public class GetOrdersQueryHandler : IRequestHandler<GetOrdersQuery, List<Order>>
{
    private readonly IApplicationDbContext _context;

    public GetOrdersQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<Order>> Handle(GetOrdersQuery request, CancellationToken cancellationToken)
    {
        var query = _context.Orders
            .Include(o => o.Client)
            .Include(o => o.DeliveryZone)
            .AsQueryable();

        if (request.BranchId.HasValue)
        {
            query = query.Where(o => o.BranchId == request.BranchId.Value);
        }

        if (request.Status.HasValue)
        {
            query = query.Where(o => o.Status == request.Status.Value);
        }

        if (request.DeliveryRouteId.HasValue)
        {
            query = query.Where(o => o.DeliveryRouteId == request.DeliveryRouteId.Value);
        }

        if (request.DeliveryZoneId.HasValue)
        {
            query = query.Where(o => o.DeliveryZoneId == request.DeliveryZoneId.Value);
        }

        if (request.LoadItems)
        {
            query = query.Include(o => o.Items).ThenInclude(i => i.Product);
        }

        return await query
            .OrderByDescending(o => o.OrderDate)
            .ToListAsync(cancellationToken);
    }
}
