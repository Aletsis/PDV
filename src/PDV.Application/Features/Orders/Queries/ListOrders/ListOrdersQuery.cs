using MediatR;
using PDV.Domain.Enums;
using PDV.Application.Common.Interfaces;
using PDV.Application.Features.Orders.Dtos;
using Microsoft.EntityFrameworkCore;

namespace PDV.Application.Features.Orders.Queries.ListOrders;

public class ListOrdersQuery : IRequest<List<OrderDto>>
{
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public bool? IsOpen { get; set; }
    public bool? IsCancelled { get; set; }
    public bool? IsConfirmed { get; set; }
    public bool? IsEnRoute { get; set; }
    public bool? IsDelivered { get; set; }
    public bool? IsNotDelivered { get; set; }
    public Guid? CashRegisterId { get; set; }
}

public class ListOrdersQueryHandler : IRequestHandler<ListOrdersQuery, List<OrderDto>>
{
    private readonly IApplicationDbContext _context;

    public ListOrdersQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<OrderDto>> Handle(ListOrdersQuery request, CancellationToken cancellationToken)
    {
        var query = _context.Orders
            .Include(o => o.Items)
            .Include(o => o.Client)
            .AsQueryable();

        if (request.StartDate.HasValue)
        {
            var start = request.StartDate.Value.Date;
            query = query.Where(s => s.OrderDate >= start);
        }

        if (request.EndDate.HasValue)
        {
            var endOfDay = request.EndDate.Value.Date.AddDays(1).AddTicks(-1);
            query = query.Where(s => s.OrderDate <= endOfDay);
        }

        if (request.IsOpen.HasValue)
        {
            query = query.Where(o => o.Status == OrderStatus.Pending);
        }

        if (request.IsCancelled.HasValue)
        {
            query = query.Where(o => o.Status == OrderStatus.Cancelled);
        }
        if (request.IsConfirmed.HasValue)
        {
            query = query.Where(o => o.Status == OrderStatus.Confirmed);
        }
        if (request.IsEnRoute.HasValue)
        {
            query = query.Where(o => o.Status == OrderStatus.EnRoute);
        }
        if (request.IsDelivered.HasValue)
        {
            query = query.Where(o => o.Status == OrderStatus.Delivered);
        }
        if (request.IsNotDelivered.HasValue)
        {
            query = query.Where(o => o.Status != OrderStatus.Delivered);
        }

        if (request.CashRegisterId.HasValue)
        {
            query = query.Where(o => o.CashRegisterId == request.CashRegisterId.Value);
        }

        return await query
            .OrderByDescending(o => o.OrderDate)
            .Select(o => new OrderDto
            {
                Id = o.Id,
                OrderNumber = $"{o.Series}-{o.Folio}",
                Date = o.OrderDate,
                TotalAmount = o.TotalAmount,
                PaymentMethod = o.PaymentMethod.ToString(),
                ClientId = o.ClientId,
                ClientName = o.Client != null ? o.Client.Name : "Público General",
                Status = o.Status,
                Channel = o.Channel,
                ItemCount = o.Items.Count,
                Series = o.Series,
                Folio = o.Folio,
                ShiftId = o.ShiftId
            })
            .ToListAsync(cancellationToken);

    }
}