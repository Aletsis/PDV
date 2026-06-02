using MediatR;
using Microsoft.EntityFrameworkCore;
using PDV.Application.Common.Interfaces;
using PDV.Application.Features.Sales.Dtos;

namespace PDV.Application.Features.Sales.Queries.GetInvoice;

public record GetInvoiceQuery(Guid InvoiceId) : IRequest<InvoiceDto?>;

public class GetInvoiceQueryHandler : IRequestHandler<GetInvoiceQuery, InvoiceDto?>
{
    private readonly IApplicationDbContext _context;

    public GetInvoiceQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<InvoiceDto?> Handle(GetInvoiceQuery request, CancellationToken cancellationToken)
    {
        var invoice = await _context.Invoices
            .Include(i => i.Sale)
            .ThenInclude(s => s!.Items)
            .ThenInclude(i => i.Product)
            .Include(i => i.Client)
            .FirstOrDefaultAsync(i => i.Id == request.InvoiceId, cancellationToken);

        if (invoice == null)
            return null;

        return new InvoiceDto
        {
            Id = invoice.Id,
            InvoiceNumber = invoice.InvoiceNumber,
            InvoiceDate = invoice.InvoiceDate,
            SaleId = invoice.SaleId,
            SaleNumber = invoice.Sale?.SaleNumber ?? string.Empty,
            ClientId = invoice.ClientId,
            ClientName = invoice.Client?.Name ?? (invoice.IsGlobal ? "Público General" : string.Empty),
            Subtotal = invoice.Subtotal,
            Tax = invoice.Tax,
            Total = invoice.Total,
            IsGlobal = invoice.IsGlobal,
            Uuid = invoice.Uuid,
            SelloDigitalEmisor = invoice.SelloDigitalEmisor,
            SelloDigitalSAT = invoice.SelloDigitalSAT,
            NoCertificadoEmisor = invoice.NoCertificadoEmisor,
            NoCertificadoSAT = invoice.NoCertificadoSAT,
            CadenaOriginal = invoice.CadenaOriginal,
            StampedAt = invoice.StampedAt
        };
    }
}
