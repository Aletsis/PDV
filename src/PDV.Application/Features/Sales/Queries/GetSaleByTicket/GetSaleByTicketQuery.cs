using MediatR;
using PDV.Application.Common.Interfaces;
using PDV.Application.Features.Sales.Dtos;
using Microsoft.EntityFrameworkCore;

namespace PDV.Application.Features.Sales.Queries.GetSaleByTicket;

public record GetSaleByTicketQuery(string Series, int Folio) : IRequest<SaleDetailDto?>;

public class GetSaleByTicketQueryHandler : IRequestHandler<GetSaleByTicketQuery, SaleDetailDto?>
{
    private readonly IApplicationDbContext _context;

    public GetSaleByTicketQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<SaleDetailDto?> Handle(GetSaleByTicketQuery request, CancellationToken cancellationToken)
    {
        var sale = await _context.Sales
            .Include(s => s.Items)
            .ThenInclude(i => i.Product)
            .Include(s => s.Client)
            .FirstOrDefaultAsync(s => s.Series == request.Series && s.Folio == request.Folio, cancellationToken);

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
            ShiftId = sale.ShiftId,
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
