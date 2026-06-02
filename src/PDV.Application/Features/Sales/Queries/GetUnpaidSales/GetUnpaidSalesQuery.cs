using MediatR;
using PDV.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace PDV.Application.Features.Sales.Queries.GetUnpaidSales;

public record GetUnpaidSalesQuery(Guid ShiftId) : IRequest<List<UnpaidSaleDto>>;

public class UnpaidSaleDto
{
    public Guid SaleId { get; set; }
    public string SaleNumber { get; set; } = string.Empty;
    public string Series { get; set; } = string.Empty;
    public int Folio { get; set; }
    public DateTime Date { get; set; }
    public decimal TotalAmount { get; set; }
    public Guid? ClientId { get; set; }
    public string ClientName { get; set; } = string.Empty;
}

public class GetUnpaidSalesQueryHandler : IRequestHandler<GetUnpaidSalesQuery, List<UnpaidSaleDto>>
{
    private readonly IApplicationDbContext _context;

    public GetUnpaidSalesQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<UnpaidSaleDto>> Handle(GetUnpaidSalesQuery request, CancellationToken cancellationToken)
    {
        var sales = await _context.Sales
            .AsNoTracking()
            .Include(s => s.Client)
            .Where(s => s.ShiftId == request.ShiftId && !s.IsPaid && !s.IsCancelled)
            .OrderByDescending(s => s.Date)
            .ToListAsync(cancellationToken);

        return sales.Select(s => new UnpaidSaleDto
        {
            SaleId = s.Id,
            SaleNumber = s.SaleNumber,
            Series = s.Series ?? string.Empty,
            Folio = s.Folio,
            Date = s.Date,
            TotalAmount = s.TotalAmount,
            ClientId = s.ClientId,
            ClientName = s.Client != null ? s.Client.Name : "Público General"
        }).ToList();
    }
}
