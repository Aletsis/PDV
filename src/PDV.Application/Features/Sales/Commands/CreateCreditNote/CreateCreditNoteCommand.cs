using MediatR;
using Microsoft.EntityFrameworkCore;
using PDV.Application.Common.Interfaces;
using PDV.Domain.Entities;
using PDV.Domain.Enums;
using PDV.Domain.ValueObjects;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace PDV.Application.Features.Sales.Commands.CreateCreditNote;

public record CreateCreditNoteCommand(Guid ReturnId) : IRequest<Guid>;

public class CreateCreditNoteCommandHandler : IRequestHandler<CreateCreditNoteCommand, Guid>
{
    private readonly IApplicationDbContext _context;
    private readonly ICsdCertificateService _csdCertificateService;
    private readonly ICfdiXmlGenerator _cfdiXmlGenerator;
    private readonly IPacService _pacService;
    private readonly IComercialApiSyncService _comercialSyncService;

    public CreateCreditNoteCommandHandler(
        IApplicationDbContext context,
        ICsdCertificateService csdCertificateService,
        ICfdiXmlGenerator cfdiXmlGenerator,
        IPacService pacService,
        IComercialApiSyncService comercialSyncService)
    {
        _context = context;
        _csdCertificateService = csdCertificateService;
        _cfdiXmlGenerator = cfdiXmlGenerator;
        _pacService = pacService;
        _comercialSyncService = comercialSyncService;
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

        var config = await _context.SystemConfigurations.FirstOrDefaultAsync(cancellationToken);
        if (config == null)
        {
            throw new InvalidOperationException("No se han configurado los parámetros fiscales del sistema.");
        }

        string pacUrl = !string.IsNullOrEmpty(config.PacUrl) ? config.PacUrl : (!string.IsNullOrEmpty(config.ComercialApiUrl) ? config.ComercialApiUrl : "https://mock-pac.sat.gob.mx");
        string pacUser = !string.IsNullOrEmpty(config.PacApiUser) ? config.PacApiUser : "CONTPAQI_PAC";
        string pacKey = config.PacApiKey ?? config.ComercialApiKey ?? string.Empty;

        // 4. Recuperar secuencia de folios de Nota de Crédito
        var creditNoteSequence = await _context.FolioSequences
            .FirstOrDefaultAsync(fs => fs.BranchId == ret.BranchId && fs.SeriesType == InvoiceType.CreditNote, cancellationToken);

        if (creditNoteSequence == null)
        {
            throw new InvalidOperationException("No se ha configurado la secuencia de folios ni el código de concepto de Nota de Crédito para esta sucursal.");
        }

        // Si la integración con CONTPAQi Comercial está activa, delegar timbrado y generación
        if (!string.IsNullOrWhiteSpace(config.ComercialApiUrl))
        {
            var origInvoiceSequence = await _context.FolioSequences
                .FirstOrDefaultAsync(fs => fs.BranchId == origInvoice.BranchId && fs.SeriesType == origInvoice.Type, cancellationToken);
            string conceptCodeDocOrigen = origInvoiceSequence?.ConceptCode ?? "FCLI";

            var ncPartidas = ret.Items.Select(item => new NotaCreditoPartidaSyncDto
            {
                CodigoProducto = item.Product?.Code ?? string.Empty,
                Unidades = (double)item.Quantity,
                PrecioUnitario = (double)item.UnitPrice,
                CodigoAlmacen = "1"
            }).ToList();

            string formaPagoNC = origSale.PaymentMethod switch
            {
                PaymentMethodType.Cash => "01",
                PaymentMethodType.CreditCard => "04",
                PaymentMethodType.DebitCard => "28",
                PaymentMethodType.Transfer => "03",
                PaymentMethodType.Check => "02",
                _ => "01"
            };

            var ncRequest = new GenerarNotaCreditoComercialDto
            {
                CodigoConcepto = creditNoteSequence.ConceptCode ?? "NC",
                Serie = creditNoteSequence.Series,
                CodigoCliente = origSale.ClientId.HasValue ? (await _context.Clients.FindAsync(new object[] { origSale.ClientId.Value }, cancellationToken))?.Code ?? "PUBLICOGENERAL" : "PUBLICOGENERAL",
                ReferenciaDocumentoOrigen = origSale.SaleNumber.ToString(),
                NumeroMoneda = 1,
                TipoCambio = 1.0,
                UsoCfdi = "G02",
                MetodoPago = "PUE",
                FormaPago = formaPagoNC,
                UuidFacturaOrigen = origInvoice.Uuid,
                TipoRelacionSat = "01",
                ConceptoFacturaOrigen = conceptCodeDocOrigen,
                SerieFacturaOrigen = origInvoice.Series,
                FolioFacturaOrigen = double.TryParse(origInvoice.Folio, out var fOrig) ? fOrig : 0,
                SaldarFacturaOrigen = true,
                CsdPassword = config.CsdPassword ?? string.Empty,
                AutoTimbrar = true,
                Partidas = ncPartidas
            };

            var ncResult = await _comercialSyncService.GenerarNotaCreditoComercialAsync(ncRequest, cancellationToken);
            if (ncResult == null || !ncResult.Timbrado || ncResult.DatosFiscales == null)
            {
                throw new InvalidOperationException($"Error al generar nota de crédito vía API Comercial: {ncResult?.Mensaje ?? "Respuesta vacía"}");
            }

            var returnTaxBreakdowns = ret.Items
                .GroupBy(i => new { i.TaxRate, i.IsTaxExempt })
                .Select(g => new TaxBreakdown(
                    Rate: g.Key.TaxRate,
                    BaseAmount: g.Sum(i => i.UnitPrice * i.Quantity),
                    TaxAmount: g.Key.IsTaxExempt ? 0 : g.Sum(i => (i.UnitPrice * i.Quantity) * (g.Key.TaxRate / 100m)),
                    IsExempt: g.Key.IsTaxExempt
                )).ToList();

            var apiCreditInvoice = Invoice.CreateCreditNote(
                branchId: ret.BranchId,
                series: creditNoteSequence.Series,
                folio: ncResult.DatosFiscales.UUID.Substring(0, 8),
                returnId: ret.Id,
                clientId: origSale.ClientId ?? Guid.Empty,
                receiverTaxId: origInvoice.ReceiverTaxId,
                receiverName: origInvoice.ReceiverName,
                relatedUuid: origInvoice.Uuid,
                subtotal: ret.Subtotal,
                taxBreakdowns: returnTaxBreakdowns,
                receiverFiscalRegime: origInvoice.ReceiverFiscalRegime,
                receiverZipCode: origInvoice.ReceiverZipCode
            );

            apiCreditInvoice.Stamp(
                uuid: ncResult.DatosFiscales.UUID,
                stampedAt: DateTime.TryParse(ncResult.DatosFiscales.FechaTimbrado, out var dtNc) ? dtNc : DateTime.UtcNow,
                selloDigitalEmisor: ncResult.DatosFiscales.SelloDigitalEmisor,
                selloDigitalSAT: ncResult.DatosFiscales.SelloDigitalSAT,
                noCertificadoEmisor: ncResult.DatosFiscales.NoCertificadoEmisor,
                noCertificadoSAT: ncResult.DatosFiscales.NoCertificadoSAT,
                cadenaOriginal: ncResult.DatosFiscales.CadenaOriginal
            );

            _context.Invoices.Add(apiCreditInvoice);

            creditNoteSequence.ResetTo(creditNoteSequence.LastFolio + 1);

            var apiShift = await _context.Shifts.FirstOrDefaultAsync(s => s.Id == ret.ShiftId, cancellationToken);
            if (apiShift != null)
            {
                apiShift.RegisterCreditNote(apiCreditInvoice.Id.ToString(), ret.TotalRefund, ret.Reason);
            }

            await _context.SaveChangesAsync(cancellationToken);

            return apiCreditInvoice.Id;
        }

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

        // 5. Generar desglose de impuestos local para la nota de crédito
        var taxBreakdowns = ret.Items
            .GroupBy(i => new { i.TaxRate, i.IsTaxExempt })
            .Select(g => new TaxBreakdown(
                Rate: g.Key.TaxRate,
                BaseAmount: g.Sum(i => i.UnitPrice * i.Quantity),
                TaxAmount: g.Key.IsTaxExempt ? 0 : g.Sum(i => (i.UnitPrice * i.Quantity) * (g.Key.TaxRate / 100m)),
                IsExempt: g.Key.IsTaxExempt
            )).ToList();

        int nextFolioNum = creditNoteSequence.LastFolio + 1;
        string folioStr = nextFolioNum.ToString();
        string seriesStr = creditNoteSequence.Series;

        // Crear la Nota de Crédito local en estado Draft
        var creditInvoice = Invoice.CreateCreditNote(
            branchId: ret.BranchId,
            series: seriesStr,
            folio: folioStr,
            returnId: ret.Id,
            clientId: origSale.ClientId ?? Guid.Empty,
            receiverTaxId: origInvoice.ReceiverTaxId,
            receiverName: origInvoice.ReceiverName,
            relatedUuid: origInvoice.Uuid,
            subtotal: ret.Subtotal,
            taxBreakdowns: taxBreakdowns,
            receiverFiscalRegime: origInvoice.ReceiverFiscalRegime,
            receiverZipCode: origInvoice.ReceiverZipCode
        );

        // Generar XML CFDI 4.0 sin firmar para la nota de crédito
        string unsignedXml = _cfdiXmlGenerator.GenerateCfdi40Xml(creditInvoice, config, "PUE", formaPago);

        // Generar Cadena Original
        string cadenaOriginal = _cfdiXmlGenerator.GenerateCadenaOriginal(unsignedXml);

        // Firmar Cadena Original (usando CSD si está cargado o sello representativo)
        string sello = string.Empty;
        if (config.CsdPrivateKeyData != null && !string.IsNullOrEmpty(config.CsdPassword))
        {
            sello = _csdCertificateService.SignCadenaOriginal(cadenaOriginal, config.CsdPrivateKeyData, config.CsdPassword);
        }
        else
        {
            sello = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes($"SELLO_NC_{creditInvoice.InvoiceNumber}_{DateTime.UtcNow.Ticks}"));
        }

        // Insertar sello en el XML
        var doc = XDocument.Parse(unsignedXml);
        doc.Root?.SetAttributeValue("Sello", sello);
        using var sw = new Utf8StringWriter();
        doc.Save(sw);
        string signedXml = sw.ToString();

        // Enviar XML al PAC para timbrado de egreso
        var stampResult = await _pacService.StampXmlAsync(
            signedXml,
            pacUser,
            pacKey,
            pacUrl,
            cancellationToken
        );

        if (!stampResult.Success || stampResult.Uuid == null)
        {
            throw new InvalidOperationException($"Error al timbrar la Nota de Crédito ante el PAC: {stampResult.ErrorMessage}");
        }

        // Estampar la Nota de Crédito en el sistema con los datos devueltos por el PAC
        creditInvoice.Stamp(
            uuid: stampResult.Uuid,
            stampedAt: stampResult.StampedAt ?? DateTime.UtcNow,
            selloDigitalEmisor: sello,
            selloDigitalSAT: stampResult.SelloSAT ?? "",
            noCertificadoEmisor: config.CsdSerialNumber ?? "00001000000500000000",
            noCertificadoSAT: stampResult.CertificadoSAT ?? "",
            cadenaOriginal: stampResult.CadenaOriginalTfd ?? ""
        );

        _context.Invoices.Add(creditInvoice);

        // Actualizar secuencia de folios
        creditNoteSequence.ResetTo(nextFolioNum);

        // Si la devolución está vinculada a un turno, registrar nota de crédito
        var shift = await _context.Shifts.FirstOrDefaultAsync(s => s.Id == ret.ShiftId, cancellationToken);
        if (shift != null)
        {
            shift.RegisterCreditNote(creditInvoice.Id.ToString(), ret.TotalRefund, ret.Reason);
        }

        await _context.SaveChangesAsync(cancellationToken);

        return creditInvoice.Id;
    }

    private class Utf8StringWriter : StringWriter
    {
        public override System.Text.Encoding Encoding => System.Text.Encoding.UTF8;
    }
}
