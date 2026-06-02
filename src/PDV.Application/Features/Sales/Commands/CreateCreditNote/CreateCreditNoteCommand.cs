using MediatR;
using Microsoft.EntityFrameworkCore;
using PDV.Application.Common.Interfaces;
using PDV.Domain.Entities;
using PDV.Domain.Enums;
using PDV.Domain.ValueObjects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace PDV.Application.Features.Sales.Commands.CreateCreditNote;

public record CreateCreditNoteCommand(Guid ReturnId) : IRequest<Guid>;

public class CreateCreditNoteCommandHandler : IRequestHandler<CreateCreditNoteCommand, Guid>
{
    private readonly IApplicationDbContext _context;
    private readonly IComercialApiSyncService _comercialApiSyncService;

    public CreateCreditNoteCommandHandler(
        IApplicationDbContext context,
        IComercialApiSyncService comercialApiSyncService)
    {
        _context = context;
        _comercialApiSyncService = comercialApiSyncService;
    }

    public async Task<Guid> Handle(CreateCreditNoteCommand request, CancellationToken cancellationToken)
    {
        // 1. Obtener la devolución
        var ret = await _context.Returns
            .Include(r => r.Items)
            .FirstOrDefaultAsync(r => r.Id == request.ReturnId, cancellationToken);

        if (ret == null)
        {
            throw new InvalidOperationException($"Devolución con ID {request.ReturnId} no encontrada.");
        }

        if (!ret.IsCompleted)
        {
            throw new InvalidOperationException("La devolución debe estar completada para emitir una Nota de Crédito.");
        }

        // Validar si ya cuenta con Nota de Crédito timbrada
        var creditNoteExists = await _context.Invoices
            .AnyAsync(i => i.ReturnId == ret.Id && i.Type == InvoiceType.CreditNote, cancellationToken);

        if (creditNoteExists)
        {
            throw new InvalidOperationException("Esta devolución ya cuenta con una Nota de Crédito generada.");
        }

        if (!ret.SaleId.HasValue)
        {
            throw new InvalidOperationException("La devolución no tiene una venta original asociada. No se puede generar Nota de Crédito.");
        }

        // 2. Obtener la venta original
        var origSale = await _context.Sales
            .FirstOrDefaultAsync(s => s.Id == ret.SaleId.Value, cancellationToken);

        if (origSale == null)
        {
            throw new InvalidOperationException("Venta original asociada no encontrada.");
        }

        // 3. Resolver el CFDI de Ingreso de referencia (individual o global)
        Invoice? origInvoice = null;
        if (origSale.IsInvoiced || origSale.InvoiceId != null)
        {
            if (Guid.TryParse(origSale.InvoiceId, out var origInvoiceGuid))
            {
                origInvoice = await _context.Invoices
                    .FirstOrDefaultAsync(i => i.Id == origInvoiceGuid && i.Status == InvoiceStatus.Stamped, cancellationToken);
            }
        }

        if (origInvoice == null || string.IsNullOrEmpty(origInvoice.Uuid))
        {
            throw new InvalidOperationException("La venta original asociada a la devolución no se encuentra facturada. Debe facturar la venta (individual o globalmente) antes de generar una Nota de Crédito.");
        }

        // 4. Recuperar secuencia de folios de Nota de Crédito
        var creditNoteSequence = await _context.FolioSequences
            .FirstOrDefaultAsync(fs => fs.BranchId == ret.BranchId && fs.SeriesType == InvoiceType.CreditNote, cancellationToken);

        if (creditNoteSequence == null || string.IsNullOrWhiteSpace(creditNoteSequence.ConceptCode))
        {
            throw new InvalidOperationException("No se ha configurado la secuencia de folios ni el código de concepto de Nota de Crédito para esta sucursal.");
        }

        // Obtener cliente
        string codCliente = "PUBLICOGENERAL";
        if (origSale.ClientId.HasValue)
        {
            var client = await _context.Clients
                .FirstOrDefaultAsync(c => c.Id == origSale.ClientId.Value, cancellationToken);
            if (client != null)
            {
                codCliente = client.Code;
            }
        }

        // 5. Mapear partidas devueltas
        var productIds = ret.Items.Select(i => i.ProductId).Distinct().ToList();
        var products = await _context.Products
            .Where(p => productIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, p => p, cancellationToken);

        var partidasDto = ret.Items.Select(item => new FacturaPartidaDto
        {
            CodigoProducto = products.TryGetValue(item.ProductId, out var product) ? product.Code : string.Empty,
            Unidades = (double)item.Quantity,
            PrecioUnitario = (double)item.UnitPrice,
            CodigoAlmacen = "1"
        }).ToList();

        // Mapear forma de pago de la venta
        string formaPago = origSale.PaymentMethod switch
        {
            PaymentMethodType.Cash => "01",
            PaymentMethodType.CreditCard => "04",
            PaymentMethodType.DebitCard => "28",
            PaymentMethodType.Transfer => "03",
            PaymentMethodType.Check => "02",
            _ => "01"
        };

        // 6. Enviar timbrado de egreso al API de Comercial
        var apiCommand = new GenerarFacturaComercialDto
        {
            CodigoConcepto = creditNoteSequence.ConceptCode,
            Serie = creditNoteSequence.Series,
            CodigoCliente = codCliente,
            Referencia = $"NC Dev Ticket {origSale.SaleNumber}",
            NumeroMoneda = 1,
            TipoCambio = 1.0,
            UsoCfdi = "G02", // Devoluciones, descuentos o bonificaciones
            MetodoPago = "PUE",
            FormaPago = formaPago,
            CsdPassword = string.Empty,
            AutoTimbrar = true,
            Partidas = partidasDto
        };

        var apiResult = await _comercialApiSyncService.GenerarFacturaComercialAsync(apiCommand, cancellationToken);

        if (apiResult == null || !apiResult.Timbrado || apiResult.DatosFiscales == null)
        {
            throw new InvalidOperationException($"No se pudo generar ni timbrar la Nota de Crédito: {apiResult?.Mensaje ?? "Error desconocido en el servidor central"}");
        }

        // 7. Crear Nota de Crédito local
        var taxBreakdowns = ret.Items
            .GroupBy(i => new { i.TaxRate, i.IsTaxExempt })
            .Select(g => new TaxBreakdown(
                Rate: g.Key.TaxRate,
                BaseAmount: g.Sum(i => i.UnitPrice * i.Quantity),
                TaxAmount: g.Key.IsTaxExempt ? 0 : g.Sum(i => (i.UnitPrice * i.Quantity) * (g.Key.TaxRate / 100m)),
                IsExempt: g.Key.IsTaxExempt
            )).ToList();

        var creditInvoice = Invoice.CreateCreditNote(
            branchId: ret.BranchId,
            series: apiResult.Serie,
            folio: apiResult.Folio,
            returnId: ret.Id,
            clientId: origSale.ClientId ?? Guid.Empty,
            receiverTaxId: origInvoice.ReceiverTaxId,
            receiverName: origInvoice.ReceiverName,
            relatedUuid: origInvoice.Uuid,
            subtotal: ret.Subtotal,
            taxBreakdowns: taxBreakdowns
        );

        DateTime ftCredit = DateTime.UtcNow;
        if (DateTime.TryParse(apiResult.DatosFiscales.FechaTimbrado, out var ftc))
        {
            ftCredit = ftc.ToUniversalTime();
        }

        creditInvoice.Stamp(
            uuid: apiResult.DatosFiscales.UUID,
            stampedAt: ftCredit,
            selloDigitalEmisor: apiResult.DatosFiscales.SelloDigitalEmisor,
            selloDigitalSAT: apiResult.DatosFiscales.SelloDigitalSAT,
            noCertificadoEmisor: apiResult.DatosFiscales.NoCertificadoEmisor,
            noCertificadoSAT: apiResult.DatosFiscales.NoCertificadoSAT,
            cadenaOriginal: apiResult.DatosFiscales.CadenaOriginal
        );

        _context.Invoices.Add(creditInvoice);

        // Actualizar secuencia de folios
        if (int.TryParse(apiResult.Folio, out var newFolio))
        {
            creditNoteSequence.ResetTo(newFolio);
        }

        // Si la devolución está vinculada a un turno activo/cerrado, registrar nota
        var shift = await _context.Shifts.FirstOrDefaultAsync(s => s.Id == ret.ShiftId, cancellationToken);
        if (shift != null)
        {
            shift.RegisterCreditNote(creditInvoice.Id.ToString(), ret.TotalRefund, ret.Reason);
        }

        await _context.SaveChangesAsync(cancellationToken);

        return creditInvoice.Id;
    }
}
