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

        var startDate = request.StartDate;
        if (startDate.HasValue)
        {
            if (startDate.Value.Kind == DateTimeKind.Local)
                startDate = startDate.Value.ToUniversalTime();
            else if (startDate.Value.Kind == DateTimeKind.Unspecified)
                startDate = DateTime.SpecifyKind(startDate.Value, DateTimeKind.Utc);
        }

        var endDate = request.EndDate;
        if (endDate.HasValue)
        {
            if (endDate.Value.Kind == DateTimeKind.Local)
                endDate = endDate.Value.ToUniversalTime();
            else if (endDate.Value.Kind == DateTimeKind.Unspecified)
                endDate = DateTime.SpecifyKind(endDate.Value, DateTimeKind.Utc);
        }

        if (startDate.HasValue)
        {
            query = query.Where(s => s.OrderDate >= startDate.Value);
        }

        if (endDate.HasValue)
        {
            query = query.Where(s => s.OrderDate <= endDate.Value);
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
                ItemCount = o.Items.Count,
                Series = o.Series,
                Folio = o.Folio,
                ShiftId = o.ShiftId
            })
            .ToListAsync(cancellationToken);

    }
}