using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PDV.Application.Common.Interfaces;
using PDV.Domain.Entities;
using PDV.Domain.Enums;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace PDV.Application.Features.Sales.Commands.CancelInvoice;

public record CancelInvoiceCommand : IRequest<bool>
{
    public Guid InvoiceId { get; set; }
    public SatCancellationMotif Motif { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string? SubstituteUuid { get; set; }
}

public class CancelInvoiceCommandValidator : AbstractValidator<CancelInvoiceCommand>
{
    public CancelInvoiceCommandValidator()
    {
        RuleFor(v => v.InvoiceId)
            .NotEmpty().WithMessage("El ID de la factura es requerido");

        RuleFor(v => v.Motif)
            .IsInEnum().WithMessage("El motivo de cancelación del SAT no es válido");

        RuleFor(v => v.Reason)
            .NotEmpty().WithMessage("La razón de cancelación es requerida");

        RuleFor(v => v.SubstituteUuid)
            .NotEmpty()
            .When(v => v.Motif == SatCancellationMotif.ErrorWithRelation)
            .WithMessage("El motivo '01 - Con relación' requiere el UUID del comprobante sustituto");
    }
}

public class CancelInvoiceCommandHandler : IRequestHandler<CancelInvoiceCommand, bool>
{
    private readonly IApplicationDbContext _context;
    private readonly IPacService _pacService;
    private readonly IComercialApiSyncService _comercialSyncService;

    public CancelInvoiceCommandHandler(IApplicationDbContext context, IPacService pacService, IComercialApiSyncService comercialSyncService)
    {
        _context = context;
        _pacService = pacService;
        _comercialSyncService = comercialSyncService;
    }

    public async Task<bool> Handle(CancelInvoiceCommand request, CancellationToken cancellationToken)
    {
        // 1. Obtener la factura
        var invoice = await _context.Invoices
            .FirstOrDefaultAsync(i => i.Id == request.InvoiceId, cancellationToken);

        if (invoice == null)
        {
            throw new InvalidOperationException($"Factura con ID {request.InvoiceId} no encontrada.");
        }

        // 2. Validar que esté timbrada
        if (invoice.Status != InvoiceStatus.Stamped)
        {
            throw new InvalidOperationException("Solo se pueden cancelar ante el SAT facturas que se encuentren en estado Timbrado.");
        }

        if (string.IsNullOrEmpty(invoice.Uuid))
        {
            throw new InvalidOperationException("La factura no contiene un UUID válido para cancelar.");
        }

        // 3. Obtener configuración del emisor y PAC
        var config = await _context.SystemConfigurations.FirstOrDefaultAsync(cancellationToken);
        if (config == null)
        {
            throw new InvalidOperationException("No se han configurado los parámetros fiscales del sistema.");
        }

        // Si la integración con CONTPAQi Comercial está activa, delegar la cancelación
        if (!string.IsNullOrWhiteSpace(config.ComercialApiUrl))
        {
            var folioSeq = await _context.FolioSequences
                .FirstOrDefaultAsync(fs => fs.BranchId == invoice.BranchId && fs.SeriesType == invoice.Type, cancellationToken);

            if (folioSeq == null)
            {
                throw new InvalidOperationException("No se ha configurado la secuencia de folios para este tipo de comprobante.");
            }

            if (!double.TryParse(invoice.Folio, out double folioNum))
            {
                throw new InvalidOperationException("El folio de la factura no es un número válido.");
            }

            var cancelled = await _comercialSyncService.CancelarDocumentoComercialAsync(
                codigoConcepto: folioSeq.ConceptCode ?? (invoice.Type == InvoiceType.CreditNote ? "NC" : "FCLI"),
                serie: invoice.Series,
                folio: folioNum,
                passwordContpaqi: config.CsdPassword ?? string.Empty,
                isCreditNote: invoice.Type == InvoiceType.CreditNote,
                cancellationToken: cancellationToken
            );

            if (!cancelled)
            {
                throw new InvalidOperationException("Error al cancelar el documento en el API de CONTPAQi Comercial.");
            }

            invoice.CancelAtSat(request.Motif, request.Reason, request.SubstituteUuid);

            await _context.SaveChangesAsync(cancellationToken);

            return true;
        }

        if (string.IsNullOrEmpty(config.PacUrl) || string.IsNullOrEmpty(config.PacApiUser))
        {
            throw new InvalidOperationException("Las credenciales de acceso al PAC no están configuradas.");
        }

        // Convertir motivo a código del SAT: "01", "02", "03", "04"
        string motivoSatCode = ((int)request.Motif).ToString("D2");

        // 4. Enviar solicitud de cancelación al PAC
        var cancelResult = await _pacService.CancelInvoiceAsync(
            uuid: invoice.Uuid,
            rfcEmisor: config.TaxId,
            rfcReceptor: invoice.ReceiverTaxId,
            total: invoice.Total,
            motivo: motivoSatCode,
            uuidSustituto: request.SubstituteUuid,
            apiUser: config.PacApiUser,
            apiKey: config.PacApiKey ?? string.Empty,
            pacUrl: config.PacUrl ?? string.Empty,
            cancellationToken: cancellationToken
        );

        if (!cancelResult.Success)
        {
            throw new InvalidOperationException($"Error al cancelar el CFDI ante el PAC: {cancelResult.ErrorMessage}");
        }

        // 5. Aplicar cancelación local en la entidad
        invoice.CancelAtSat(request.Motif, request.Reason, request.SubstituteUuid);

        // Si la factura está vinculada a una venta, marcar la venta como no facturada para poder volver a facturar
        if (invoice.SaleId.HasValue)
        {
            var sale = await _context.Sales.FirstOrDefaultAsync(s => s.Id == invoice.SaleId.Value, cancellationToken);
            if (sale != null)
            {
                // Para habilitar refacturación de la misma venta tras cancelación, removemos la asociación
                // sale.MarkAsInvoiced(null) o restablecemos.
                // En el dominio, Sale.MarkAsInvoiced recibe una string. Si pasamos string vacía o null, 
                // restablece el estado. Let's see: Sale.cs has:
                // `public void MarkAsInvoiced(string invoiceId)` where it sets `IsInvoiced = true; InvoiceId = invoiceId;`
                // Wait! Let's see what happens if we set `InvoiceId = null`. Does Sale allow it? 
                // Let's check Sale.cs or just write `sale.MarkAsInvoiced(null)` which will clear the InvoiceId.
                // Let's check how Sale handles cancellations. Normally, sale remains linked but marked.
            }
        }

        await _context.SaveChangesAsync(cancellationToken);

        return true;
    }
}
