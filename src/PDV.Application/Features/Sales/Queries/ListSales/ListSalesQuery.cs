using MediatR;
using PDV.Application.Common.Interfaces;
using PDV.Application.Features.Sales.Dtos;
using Microsoft.EntityFrameworkCore;

namespace PDV.Application.Features.Sales.Queries.ListSales;

public record ListSalesQuery(DateTime? StartDate = null, DateTime? EndDate = null, bool? IsPaid = null, bool? IsCancelled = null, Guid? CashRegisterId = null) : IRequest<List<SaleDto>>;

public class ListSalesQueryHandler : IRequestHandler<ListSalesQuery, List<SaleDto>>
{
    private readonly IApplicationDbContext _context;

    public ListSalesQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<SaleDto>> Handle(ListSalesQuery request, CancellationToken cancellationToken)
    {
        var query = _context.Sales
            .Include(s => s.Items)
            .Include(s => s.Client)
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
            query = query.Where(s => s.Date >= startDate.Value);
        }

        if (endDate.HasValue)
        {
            query = query.Where(s => s.Date <= endDate.Value);
        }

        if (request.IsPaid.HasValue)
        {
            query = query.Where(s => s.IsPaid == request.IsPaid.Value);
        }

        if (request.IsCancelled.HasValue)
        {
            query = query.Where(s => s.IsCancelled == request.IsCancelled.Value);
        }

        if (request.CashRegisterId.HasValue)
        {
            query = query.Where(s => s.CashRegisterId == request.CashRegisterId.Value);
        }

        return await query
            .OrderByDescending(s => s.Date)
            .Select(s => new SaleDto
            {
                Id = s.Id,
                SaleNumber = s.SaleNumber,
                Date = s.Date,
                TotalAmount = s.TotalAmount,
                PaymentMethod = s.PaymentMethod.ToString(),
                ClientId = s.ClientId,
                ClientName = s.Client != null ? s.Client.Name : "Público General",
                IsPaid = s.IsPaid,
                IsCancelled = s.IsCancelled,
                IsReturned = s.IsReturned,
                ItemCount = s.Items.Count,
                Series = s.Series,
                Folio = s.Folio
            })
            .ToListAsync(cancellationToken);
    }
}
