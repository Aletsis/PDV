using MediatR;
using PDV.Application.Common.Interfaces;
using PDV.Application.Features.Sales.Dtos;
using Microsoft.EntityFrameworkCore;
using PDV.Domain.Enums;

namespace PDV.Application.Features.Sales.Queries.GetDraftSale;

public record GetDraftSaleQuery(Guid SaleId) : IRequest<DraftSaleDto?>;

public class DraftSaleDto
{
    public Guid SaleId { get; set; }
    public Guid? ClientId { get; set; }
    public List<CartItemDto> Items { get; set; } = new();
}

public class GetDraftSaleQueryHandler : IRequestHandler<GetDraftSaleQuery, DraftSaleDto?>
{
    private readonly IApplicationDbContext _context;

    public GetDraftSaleQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<DraftSaleDto?> Handle(GetDraftSaleQuery request, CancellationToken cancellationToken)
    {
        var sale = await _context.Sales
            .AsNoTracking()
            .Include(s => s.Items)
            .ThenInclude(i => i.Product)
            .FirstOrDefaultAsync(s => s.Id == request.SaleId, cancellationToken);

        if (sale == null) return null;

        var dto = new DraftSaleDto
        {
            SaleId = sale.Id,
            ClientId = sale.ClientId,
            Items = sale.Items.Select(i => new CartItemDto
            {
                Product = i.Product,
                SaleItemId = i.Id,
                Quantity = i.Product.SaleType == SaleType.Bulk ? 0 : (int)i.Quantity,
                Weight = i.Product.SaleType == SaleType.Bulk ? i.Quantity : 0m,
                PriceOverride = i.PriceOverride
            }).ToList()
        };

        return dto;
    }
}
