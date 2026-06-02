using MediatR;
using PDV.Application.Common.Interfaces;
using PDV.Application.Features.Sales.Dtos;
using Microsoft.EntityFrameworkCore;

namespace PDV.Application.Features.Sales.Queries.GetSale;

public record GetSaleQuery(Guid Id) : IRequest<SaleDetailDto?>;

public class GetSaleQueryHandler : IRequestHandler<GetSaleQuery, SaleDetailDto?>
{
    private readonly IApplicationDbContext _context;

    public GetSaleQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<SaleDetailDto?> Handle(GetSaleQuery request, CancellationToken cancellationToken)
    {
        var sale = await _context.Sales
            .Include(s => s.Items)
            .ThenInclude(i => i.Product)
            .Include(s => s.Client)
            .FirstOrDefaultAsync(s => s.Id == request.Id, cancellationToken);

        if (sale == null)
            return null;

        return new SaleDetailDto
        {
            Id = sale.Id,
            SaleNumber = sale.SaleNumber,
            Date = sale.Date,
            TotalAmount = sale.TotalAmount,
            PaymentMethod = sale.PaymentMethod.ToString(),
            ClientId = sale.ClientId,
            ClientName = sale.Client != null ? sale.Client.Name : "Público General",
            IsPaid = sale.IsPaid,
            IsCancelled = sale.IsCancelled,
            IsReturned = sale.IsReturned,
            Series = sale.Series,
            Folio = sale.Folio,
            Items = sale.Items.Select(i => new SaleItemDto
            {
                Id = i.Id,
                ProductId = i.ProductId,
                ProductName = i.ProductName,
                UnitPrice = i.UnitPrice,
                PriceOverride = i.PriceOverride,
                Quantity = i.Quantity,
                TotalPrice = i.TotalAmount,
                IsReturned = i.IsReturned
            }).ToList()
        };
    }
}
