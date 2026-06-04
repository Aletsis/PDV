using MediatR;
using PDV.Application.Common.Interfaces;
using PDV.Application.Features.Sales.Dtos;
using Microsoft.EntityFrameworkCore;

namespace PDV.Application.Features.Sales.Queries.ListInvoices;

public record ListInvoicesQuery(DateTime? StartDate = null, DateTime? EndDate = null, bool? IsGlobal = null, Guid? ClientId = null) : IRequest<List<InvoiceDto>>;

public class ListInvoicesQueryHandler : IRequestHandler<ListInvoicesQuery, List<InvoiceDto>>
{
    private readonly IApplicationDbContext _context;

    public ListInvoicesQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<InvoiceDto>> Handle(ListInvoicesQuery request, CancellationToken cancellationToken)
    {
        var query = _context.Invoices
            .Include(i => i.Sale)
            .Include(i => i.Client)
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
            query = query.Where(i => i.InvoiceDate >= startDate.Value);
        }

        if (endDate.HasValue)
        {
            query = query.Where(i => i.InvoiceDate <= endDate.Value);
        }

        if (request.IsGlobal.HasValue)
        {
            query = query.Where(i => i.IsGlobal == request.IsGlobal.Value);
        }

        if (request.ClientId.HasValue)
        {
            query = query.Where(i => i.ClientId == request.ClientId.Value);
        }

        return await query
            .OrderByDescending(i => i.InvoiceDate)
            .Select(i => new InvoiceDto
            {
                Id = i.Id,
                InvoiceNumber = i.InvoiceNumber,
                InvoiceDate = i.InvoiceDate,
                SaleId = i.SaleId,
                SaleNumber = i.Sale != null ? i.Sale.SaleNumber : string.Empty,
                ClientId = i.ClientId,
                ClientName = i.Client != null ? i.Client.Name : (i.IsGlobal ? "Público General" : string.Empty),
                Subtotal = i.Subtotal,
                Tax = i.Tax,
                Total = i.Total,
                IsGlobal = i.IsGlobal,
                Uuid = i.Uuid,
                StampedAt = i.StampedAt
            })
            .ToListAsync(cancellationToken);
    }
}
