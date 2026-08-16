using MediatR;
using PDV.Application.Common.Interfaces;
using PDV.Application.Features.Orders.Dtos;
using Microsoft.EntityFrameworkCore;
using PDV.Domain.Entities;

namespace PDV.Application.Features.Orders.Queries.GetOrderByTicket;

public record GetOrderByTicketQuery(string Series, int Folio) : IRequest<OrderDetailDto?>;

public class GetOrderByTicketQueryHandler : IRequestHandler<GetOrderByTicketQuery, OrderDetailDto?>
{
    private readonly IApplicationDbContext _context;

    public GetOrderByTicketQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<OrderDetailDto?> Handle(GetOrderByTicketQuery request, CancellationToken cancellationToken)
    {
        var order = await _context.Orders
            .Include(o => o.Items)
            .ThenInclude(i => i.Product)
            .Include(o => o.Client)
            .FirstOrDefaultAsync(o => o.Series == request.Series && o.Folio == request.Folio, cancellationToken);

        if (order == null)
            return null;

        return new OrderDetailDto
        {
            Id = order.Id,
            OrderNumber = $"{order.Series}-{order.Folio}",
            Date = order.OrderDate,
            TotalAmount = order.TotalAmount,
            PaymentMethod = order.PaymentMethod.ToString(),
            ClientId = order.ClientId,
            ClientName = order.Client != null ? order.Client.Name : "Público General",
            IsCancelled = order.IsCancelled,
            Status = order.Status,
            Series = order.Series,
            Folio = order.Folio,
            ShiftId = order.ShiftId,
            Items = order.Items.Select(i => new OrderItemDto
            {
                Id = i.Id,
                ProductId = i.ProductId,
                ProductName = i.ProductName,
                UnitPrice = i.UnitPrice,
                PriceOverride = i.Product != null && i.UnitPrice != i.Product.Price ? i.UnitPrice : null,
                Quantity = i.Quantity,
                TotalPrice = i.TotalAmount,
                IsReturned = false
            }).ToList()
        };
    }
}