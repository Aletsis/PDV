using MediatR;
using PDV.Application.Common.Interfaces;
using PDV.Application.Features.Sales.Dtos;
using Microsoft.EntityFrameworkCore;

namespace PDV.Application.Features.Sales.Queries.ListSales;

public record ListSalesQuery(
    DateTime? StartDate = null,
    DateTime? EndDate = null,
    bool? IsPaid = null,
    bool? IsCancelled = null,
    Guid? CashRegisterId = null,
    Guid? BranchId = null,
    bool? IsInvoiced = null) : IRequest<List<SaleDto>>;

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

        if (request.StartDate.HasValue)
        {
            var start = request.StartDate.Value.Date;
            query = query.Where(s => s.Date >= start);
        }

        if (request.EndDate.HasValue)
        {
            var endOfDay = request.EndDate.Value.Date.AddDays(1).AddTicks(-1);
            query = query.Where(s => s.Date <= endOfDay);
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

        if (request.BranchId.HasValue && request.BranchId.Value != Guid.Empty)
        {
            query = query.Where(s => s.BranchId == request.BranchId.Value);
        }

        if (request.IsInvoiced.HasValue)
        {
            query = query.Where(s => s.IsInvoiced == request.IsInvoiced.Value);
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
                Folio = s.Folio,
                BranchId = s.BranchId,
                IsInvoiced = s.IsInvoiced,
                InvoiceId = s.InvoiceId
            })
            .ToListAsync(cancellationToken);
    }
}
