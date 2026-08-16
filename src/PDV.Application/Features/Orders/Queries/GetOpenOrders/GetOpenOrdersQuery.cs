using MediatR;
using Microsoft.EntityFrameworkCore;
using PDV.Application.Common.Interfaces;
using PDV.Domain.Enums;

namespace PDV.Application.Features.Orders.Queries.GetOpenOrders;

public record GetOpenOrdersQuery(Guid ShiftId) : IRequest<List<OpenOrderDto>>;

public class OpenOrderDto
{
    public Guid OrderId { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public string Series { get; set; } = string.Empty;
    public int Folio { get; set; }
    public DateTime Date { get; set; }
    public decimal TotalAmount { get; set; }
    public Guid? ClientId { get; set; }
    public string ClientName { get; set; } = string.Empty;
}

public class GetOpenOrdersQueryHandler : IRequestHandler<GetOpenOrdersQuery, List<OpenOrderDto>>
{
    private readonly IApplicationDbContext _context;

    public GetOpenOrdersQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<OpenOrderDto>> Handle(GetOpenOrdersQuery request, CancellationToken cancellationToken)
    {
        var shift = await _context.Shifts
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == request.ShiftId, cancellationToken);
            
        if (shift == null) return new List<OpenOrderDto>();

        var orders = await _context.Orders
            .AsNoTracking()
            .Include(o => o.Client)
            .Where(o => o.CashRegisterId == shift.CashRegisterId && o.Status == OrderStatus.Pending)
            .OrderByDescending(o => o.OrderDate)
            .ToListAsync(cancellationToken);


        return orders.Select(o => new OpenOrderDto
        {
            OrderId = o.Id,
            OrderNumber = $"{o.Series}-{o.Folio}",
            Series = o.Series ?? string.Empty,
            Folio = o.Folio,
            Date = o.OrderDate,
            TotalAmount = o.TotalAmount,
            ClientId = o.ClientId,
            ClientName = o.Client != null ? o.Client.Name : "Público General"
        }).ToList();
    }
}