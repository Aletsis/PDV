using MediatR;
using Microsoft.EntityFrameworkCore;
using PDV.Domain.Enums;
using PDV.Application.Common.Interfaces;
using PDV.Application.Features.Orders.Dtos;

namespace PDV.Application.Features.Orders.Queries.GetDraftOrder;

public record GetDraftOrderQuery(Guid OrderId) : IRequest<DraftOrderDto?>;

public class DraftOrderDto
{
    public Guid OrderId { get; set; }
    public Guid? ClientId { get; set; }
    public List<CartItemDto> Items { get; set; } = new();
}

public class GetDraftOrderQueryHandler : IRequestHandler<GetDraftOrderQuery, DraftOrderDto?>
{
    private readonly IApplicationDbContext _context;

    public GetDraftOrderQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }
    public async Task<DraftOrderDto?> Handle(GetDraftOrderQuery request, CancellationToken cancellationToken)
    {
        var order = await _context.Orders
            .AsNoTracking()
            .Include(o => o.Items)
            .ThenInclude(i => i.Product)
            .FirstOrDefaultAsync(o => o.Id == request.OrderId, cancellationToken);

        if (order == null) return null;

        var dto = new DraftOrderDto
        {
            OrderId = order.Id,
            ClientId = order.ClientId,
            Items = order.Items.Select(i => new CartItemDto
            {
                Product = i.Product!,
                SaleItemId = i.Id,
                OrderItemId = i.Id,
                Quantity = i.Product?.SaleType == SaleType.Bulk ? 0 : (int)i.Quantity,
                Weight = i.Product?.SaleType == SaleType.Bulk ? i.Quantity : 0m,
                PriceOverride = i.Product != null && i.UnitPrice != i.Product.Price ? i.UnitPrice : null,
                Notes = i.Notes,
                RequestedQuantity = i.RequestedQuantity,
                IsFulfilled = i.IsFulfilled
            }).ToList()
        };

        return dto;
    }
}