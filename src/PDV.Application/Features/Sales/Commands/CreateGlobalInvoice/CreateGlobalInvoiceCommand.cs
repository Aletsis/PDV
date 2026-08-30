using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
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

namespace PDV.Application.Features.Sales.Commands.CreateGlobalInvoice;

public record CreateGlobalInvoiceCommand(
    Guid ShiftId, 
    string CodigoProductoGravado = "01010101", 
    string CodigoProductoExento = "01010101"
) : IRequest<Guid>;

public class CreateGlobalInvoiceCommandHandler : IRequestHandler<CreateGlobalInvoiceCommand, Guid>
{
    private readonly IApplicationDbContext _context;
    private readonly ICsdCertificateService _csdCertificateService;
    private readonly ICfdiXmlGenerator _cfdiXmlGenerator;
    private readonly IPacService _pacService;
    private readonly ILogger<CreateGlobalInvoiceCommandHandler> _logger;
    private readonly IComercialApiSyncService _comercialSyncService;

    public CreateGlobalInvoiceCommandHandler(
        IApplicationDbContext context,
        ICsdCertificateService csdCertificateService,
        ICfdiXmlGenerator cfdiXmlGenerator,
        IPacService pacService,
        ILogger<CreateGlobalInvoiceCommandHandler> logger,
        IComercialApiSyncService comercialSyncService)
    {
        _context = context;
        _csdCertificateService = csdCertificateService;
        _cfdiXmlGenerator = cfdiXmlGenerator;
        _pacService = pacService;
        _logger = logger;
        _comercialSyncService = comercialSyncService;
    }

    public async Task<Guid> Handle(CreateGlobalInvoiceCommand request, CancellationToken cancellationToken)
    {
        // 1. Obtener el turno cerrado
        var shift = await _context.Shifts
            .FirstOrDefaultAsync(s => s.Id == request.ShiftId, cancellationToken);

        if (shift == null)
        {
            throw new InvalidOperationException($"Turno con ID {request.ShiftId} no encontrado.");
        }

        if (shift.Status != ShiftStatus.Closed)
        {
            throw new InvalidOperationException("El turno debe estar cerrado para generar la factura global.");
        }

        if (shift.IsGlobalInvoiced)
        {
            throw new InvalidOperationException("El turno ya cuenta con una factura global generada.");
        }

        // Obtener la sucursal de la caja del turno
        var cashRegister = await _context.CashRegisters
            .FirstOrDefaultAsync(c => c.Id == shift.CashRegisterId, cancellationToken);
        if (cashRegister == null)
        {
            throw new InvalidOperationException("Caja registradora asociada al turno no encontrada.");
        }
        var branchId = cashRegister.BranchId;

        // 2. Obtener todas las ventas pagadas, no canceladas y no facturadas
        var sales = await _context.Sales
            .Include(s => s.Items)
            .Where(s => s.ShiftId == shift.Id && s.IsPaid && !s.IsCancelled && !s.IsInvoiced && s.InvoiceId == null)
            .ToListAsync(cancellationToken);

        if (!sales.Any())
        {
            throw new InvalidOperationException("No existen ventas pendientes de facturar en este turno.");
        }

        var config = await _context.SystemConfigurations.FirstOrDefaultAsync(cancellationToken);
        if (config == null)
        {
            throw new InvalidOperationException("No se han configurado los parámetros fiscales del sistema.");
        }

        // Si la integración con CONTPAQi Comercial está activa, delegar timbrado y generación
        if (!string.IsNullOrWhiteSpace(config.ComercialApiUrl))
        {
            // 3. Recuperar secuencia de folios para facturas globales
            var apiFolioSequence = await _context.FolioSequences
                .FirstOrDefaultAsync(fs => fs.BranchId == branchId && fs.SeriesType == InvoiceType.Global, cancellationToken);

            if (apiFolioSequence == null)
            {
                throw new InvalidOperationException("No se ha configurado la secuencia de folios para Factura Global en esta sucursal.");
            }

            // Mapear los Conceptos
            var apiConceptos = new List<ConceptoGlobalDto>();
            foreach (var sale in sales)
            {
                var traslados = sale.Items
                    .GroupBy(i => new { i.TaxRate, i.IsTaxExempt })
                    .Select(g => new TrasladoConceptoDto
                    {
                        Base = (double)g.Sum(i => i.UnitPrice * i.Quantity),
                        Impuesto = "002",
                        TipoFactor = g.Key.IsTaxExempt ? "Exento" : "Tasa",
                        TasaOCuota = g.Key.IsTaxExempt ? "0.000000" : (g.Key.TaxRate / 100m).ToString("F6", System.Globalization.CultureInfo.InvariantCulture),
                        Importe = g.Key.IsTaxExempt ? 0 : (double)g.Sum(i => (i.UnitPrice * i.Quantity) * (g.Key.TaxRate / 100m))
                    }).ToList();

                apiConceptos.Add(new ConceptoGlobalDto
                {
                    NoIdentificacion = sale.SaleNumber.ToString(),
                    ValorUnitario = (double)sale.Subtotal,
                    Importe = (double)sale.Subtotal,
                    Traslados = traslados
                });
            }

            var apiRequest = new CreateFacturaGlobalCommandDto
            {
                CodigoConcepto = apiFolioSequence.ConceptCode ?? "FGIH",
                Serie = apiFolioSequence.Series,
                CodigoClientePublicoGeneral = "PUBLICOGENERAL",
                Periodicidad = "01",
                Meses = DateTime.Now.Month.ToString("D2"),
                Anio = DateTime.Now.Year.ToString(),
                UsoCfdi = "S01",
                MetodoPago = "PUE",
                FormaPago = "01",
                CodigoProductoGravado = request.CodigoProductoGravado,
                CodigoProductoExento = request.CodigoProductoExento,
                AutoTimbrar = true,
                CodigoAlmacen = "1",
                Conceptos = apiConceptos
            };

            _logger.LogInformation("Enviando timbrado de Factura Global para Turno {ShiftId} al API Comercial...", shift.Id);
            var apiResult = await _comercialSyncService.GenerarFacturaGlobalComercialAsync(apiRequest, cancellationToken);
            if (apiResult == null || !apiResult.Timbrado || apiResult.DatosFiscales == null)
            {
                throw new InvalidOperationException($"Error al generar factura global vía API Comercial: {apiResult?.Mensaje ?? "Respuesta vacía"}");
            }

            var localTaxBreakdowns = sales.SelectMany(s => s.Items)
                .GroupBy(i => new { i.TaxRate, i.IsTaxExempt })
                .Select(g => new TaxBreakdown(
                    Rate: g.Key.TaxRate,
                    BaseAmount: g.Sum(i => i.UnitPrice * i.Quantity),
                    TaxAmount: g.Key.IsTaxExempt ? 0 : g.Sum(i => (i.UnitPrice * i.Quantity) * (g.Key.TaxRate / 100m)),
                    IsExempt: g.Key.IsTaxExempt
                )).ToList();

            var apiGlobalInvoice = Invoice.CreateGlobalInvoice(
                branchId: branchId,
                series: apiResult.Serie,
                folio: apiResult.Folio,
                shiftId: shift.Id,
                subtotal: sales.Sum(s => s.Subtotal),
                taxBreakdowns: localTaxBreakdowns,
                receiverZipCode: config.FiscalAddress?.ZipCode ?? "00000"
            );

            apiGlobalInvoice.Stamp(
                uuid: apiResult.DatosFiscales.UUID,
                stampedAt: DateTime.TryParse(apiResult.DatosFiscales.FechaTimbrado, out var dt) ? dt : DateTime.Now,
                selloDigitalEmisor: apiResult.DatosFiscales.SelloDigitalEmisor,
                selloDigitalSAT: apiResult.DatosFiscales.SelloDigitalSAT,
                noCertificadoEmisor: apiResult.DatosFiscales.NoCertificadoEmisor,
                noCertificadoSAT: apiResult.DatosFiscales.NoCertificadoSAT,
                cadenaOriginal: apiResult.DatosFiscales.CadenaOriginal
            );

            _context.Invoices.Add(apiGlobalInvoice);

            if (int.TryParse(apiResult.Folio, out var parsedFolio))
            {
                apiFolioSequence.ResetTo(parsedFolio);
            }

            shift.MarkAsGlobalInvoiced(apiGlobalInvoice.Id.ToString());

            foreach (var sale in sales)
            {
                sale.MarkAsInvoiced(apiGlobalInvoice.Id.ToString());
            }

            await _context.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Factura Global {InvoiceNum} timbrada e integrada vía API exitosamente.", apiGlobalInvoice.InvoiceNumber);

            // PROCESAMIENTO AUTOMÁTICO DE NOTAS DE CRÉDITO DE DEVOLUCIONES VÍA API
            var returnsList = await _context.Returns
                .Include(r => r.Items)
                .Where(r => r.ShiftId == shift.Id && r.IsCompleted)
                .ToListAsync(cancellationToken);

            if (returnsList.Any())
            {
                _logger.LogInformation("Procesando {Count} devoluciones del turno para generar Notas de Crédito de forma automática vía API...", returnsList.Count);

                var creditNoteSequence = await _context.FolioSequences
                    .FirstOrDefaultAsync(fs => fs.BranchId == branchId && fs.SeriesType == InvoiceType.CreditNote, cancellationToken);

                if (creditNoteSequence == null)
                {
                    _logger.LogWarning("Secuencia de folios para Nota de Crédito no configurada para la sucursal. Se omitirán las Notas de Crédito automáticas.");
                }
                else
                {
                    foreach (var ret in returnsList)
                    {
                        try
                        {
                            var creditNoteExists = await _context.Invoices
                                .AnyAsync(i => i.ReturnId == ret.Id && i.Type == InvoiceType.CreditNote, cancellationToken);

                            if (creditNoteExists) continue;

                            if (!ret.SaleId.HasValue) continue;

                            var origSale = await _context.Sales
                                .FirstOrDefaultAsync(s => s.Id == ret.SaleId.Value, cancellationToken);

                            if (origSale == null) continue;

                            Invoice? origInvoice = null;
                            if (origSale.IsInvoiced || origSale.InvoiceId != null)
                            {
                                if (Guid.TryParse(origSale.InvoiceId, out var origInvoiceGuid))
                                {
                                    origInvoice = await _context.Invoices
                                        .FirstOrDefaultAsync(i => i.Id == origInvoiceGuid && i.Status == InvoiceStatus.Stamped, cancellationToken);
                                }
                            }

                            if (origInvoice == null)
                            {
                                origInvoice = apiGlobalInvoice;
                            }

                            if (origInvoice == null || string.IsNullOrEmpty(origInvoice.Uuid)) continue;

                            var origInvoiceSequence = await _context.FolioSequences
                                .FirstOrDefaultAsync(fs => fs.BranchId == origInvoice.BranchId && fs.SeriesType == origInvoice.Type, cancellationToken);
                            string conceptCodeDocOrigen = origInvoiceSequence?.ConceptCode ?? "FCLI";

                            string formaPagoNC = origSale.PaymentMethod switch
                            {
                                PaymentMethodType.Cash => "01",
                                PaymentMethodType.CreditCard => "04",
                                PaymentMethodType.DebitCard => "28",
                                PaymentMethodType.Transfer => "03",
                                PaymentMethodType.Check => "02",
                                _ => "01"
                            };

                            var ncPartidas = ret.Items.Select(item => new NotaCreditoPartidaSyncDto
                            {
                                CodigoProducto = item.Product?.Code ?? string.Empty,
                                Unidades = (double)item.Quantity,
                                PrecioUnitario = (double)item.UnitPrice,
                                CodigoAlmacen = "1"
                            }).ToList();

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
                            if (ncResult != null && ncResult.Timbrado && ncResult.DatosFiscales != null)
                            {
                                var returnTaxBreakdowns = ret.Items
                                    .GroupBy(i => new { i.TaxRate, i.IsTaxExempt })
                                    .Select(g => new TaxBreakdown(
                                        Rate: g.Key.TaxRate,
                                        BaseAmount: g.Sum(i => i.UnitPrice * i.Quantity),
                                        TaxAmount: g.Key.IsTaxExempt ? 0 : g.Sum(i => (i.UnitPrice * i.Quantity) * (g.Key.TaxRate / 100m)),
                                        IsExempt: g.Key.IsTaxExempt
                                    )).ToList();

                                var apiCreditInvoice = Invoice.CreateCreditNote(
                                    branchId: branchId,
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
                                    stampedAt: DateTime.TryParse(ncResult.DatosFiscales.FechaTimbrado, out var dtNc) ? dtNc : DateTime.Now,
                                    selloDigitalEmisor: ncResult.DatosFiscales.SelloDigitalEmisor,
                                    selloDigitalSAT: ncResult.DatosFiscales.SelloDigitalSAT,
                                    noCertificadoEmisor: ncResult.DatosFiscales.NoCertificadoEmisor,
                                    noCertificadoSAT: ncResult.DatosFiscales.NoCertificadoSAT,
                                    cadenaOriginal: ncResult.DatosFiscales.CadenaOriginal
                                );

                                _context.Invoices.Add(apiCreditInvoice);

                                creditNoteSequence.ResetTo(creditNoteSequence.LastFolio + 1);

                                shift.RegisterCreditNote(apiCreditInvoice.Id.ToString(), ret.TotalRefund, ret.Reason);
                                await _context.SaveChangesAsync(cancellationToken);
                                _logger.LogInformation("Nota de Crédito {NCNumber} timbrada vía API de forma automática.", apiCreditInvoice.InvoiceNumber);
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error al generar Nota de Crédito automática vía API para la devolución {ReturnId}.", ret.Id);
                        }
                    }
                }
            }

            return apiGlobalInvoice.Id;
        }

        string pacUrl = !string.IsNullOrEmpty(config.PacUrl) ? config.PacUrl : (!string.IsNullOrEmpty(config.ComercialApiUrl) ? config.ComercialApiUrl : "https://mock-pac.sat.gob.mx");
        string pacUser = !string.IsNullOrEmpty(config.PacApiUser) ? config.PacApiUser : "CONTPAQI_PAC";
        string pacKey = config.PacApiKey ?? config.ComercialApiKey ?? string.Empty;

        // 3. Recuperar secuencia de folios para facturas globales
        var folioSequence = await _context.FolioSequences
            .FirstOrDefaultAsync(fs => fs.BranchId == branchId && fs.SeriesType == InvoiceType.Global, cancellationToken);

        if (folioSequence == null)
        {
            throw new InvalidOperationException("No se ha configurado la secuencia de folios para Factura Global en esta sucursal.");
        }

        // 4. Generar desglose de impuestos local
        var taxBreakdowns = sales.SelectMany(s => s.Items)
            .GroupBy(i => new { i.TaxRate, i.IsTaxExempt })
            .Select(g => new TaxBreakdown(
                Rate: g.Key.TaxRate,
                BaseAmount: g.Sum(i => i.UnitPrice * i.Quantity),
                TaxAmount: g.Key.IsTaxExempt ? 0 : g.Sum(i => (i.UnitPrice * i.Quantity) * (g.Key.TaxRate / 100m)),
                IsExempt: g.Key.IsTaxExempt
            )).ToList();

        int nextFolioNum = folioSequence.LastFolio + 1;
        string folioStr = nextFolioNum.ToString();
        string seriesStr = folioSequence.Series;

        // 5. Crear la Factura Global en nuestra base de datos local (Draft)
        var globalInvoice = Invoice.CreateGlobalInvoice(
            branchId: branchId,
            series: seriesStr,
            folio: folioStr,
            shiftId: shift.Id,
            subtotal: sales.Sum(s => s.Subtotal),
            taxBreakdowns: taxBreakdowns,
            receiverZipCode: config.FiscalAddress?.ZipCode ?? "00000"
        );

        // Generar XML CFDI 4.0 sin firmar para la factura global
        string unsignedXml = _cfdiXmlGenerator.GenerateCfdi40Xml(globalInvoice, config, "PUE", "01");

        // Generar Cadena Original
        string cadenaOriginal = _cfdiXmlGenerator.GenerateCadenaOriginal(unsignedXml);

        // Firmar Cadena Original (usando CSD si está cargado o sello representativo generado para el PAC/CONTPAQi)
        string sello = string.Empty;
        if (config.CsdPrivateKeyData != null && !string.IsNullOrEmpty(config.CsdPassword))
        {
            sello = _csdCertificateService.SignCadenaOriginal(cadenaOriginal, config.CsdPrivateKeyData, config.CsdPassword);
        }
        else
        {
            sello = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes($"SELLO_GLOBAL_{globalInvoice.InvoiceNumber}_{DateTime.Now.Ticks}"));
        }

        // Insertar sello en el XML
        var doc = XDocument.Parse(unsignedXml);
        doc.Root?.SetAttributeValue("Sello", sello);
        using var sw = new Utf8StringWriter();
        doc.Save(sw);
        string signedXml = sw.ToString();

        // Enviar XML al PAC para timbrado global
        _logger.LogInformation("Enviando timbrado de Factura Global para Turno {ShiftId} al PAC...", shift.Id);
        var stampResult = await _pacService.StampXmlAsync(
            signedXml,
            pacUser,
            pacKey,
            pacUrl,
            cancellationToken
        );

        if (!stampResult.Success || stampResult.Uuid == null)
        {
            throw new InvalidOperationException($"Error al timbrar la Factura Global ante el PAC: {stampResult.ErrorMessage}");
        }

        // Estampar la Factura Global con los datos del PAC
        globalInvoice.Stamp(
            uuid: stampResult.Uuid,
            stampedAt: stampResult.StampedAt ?? DateTime.Now,
            selloDigitalEmisor: sello,
            selloDigitalSAT: stampResult.SelloSAT ?? "",
            noCertificadoEmisor: config.CsdSerialNumber ?? "00001000000500000000",
            noCertificadoSAT: stampResult.CertificadoSAT ?? "",
            cadenaOriginal: stampResult.CadenaOriginalTfd ?? ""
        );

        _context.Invoices.Add(globalInvoice);

        // Actualizar secuencia de folios
        folioSequence.ResetTo(nextFolioNum);

        // Registrar en el turno y en las ventas asociadas
        shift.MarkAsGlobalInvoiced(globalInvoice.Id.ToString());

        foreach (var sale in sales)
        {
            sale.MarkAsInvoiced(globalInvoice.Id.ToString());
        }

        await _context.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Factura Global {InvoiceNum} timbrada e integrada exitosamente.", globalInvoice.InvoiceNumber);

        // 6. PROCESAMIENTO AUTOMÁTICO DE NOTAS DE CRÉDITO DE DEVOLUCIONES
        var returns = await _context.Returns
            .Include(r => r.Items)
            .Where(r => r.ShiftId == shift.Id && r.IsCompleted)
            .ToListAsync(cancellationToken);

        if (returns.Any())
        {
            _logger.LogInformation("Procesando {Count} devoluciones del turno para generar Notas de Crédito de forma automática...", returns.Count);

            // Recuperar secuencia de folios para Notas de Crédito una vez fuera del ciclo
            var creditNoteSequence = await _context.FolioSequences
                .FirstOrDefaultAsync(fs => fs.BranchId == branchId && fs.SeriesType == InvoiceType.CreditNote, cancellationToken);

            if (creditNoteSequence == null)
            {
                _logger.LogWarning("Secuencia de folios para Nota de Crédito no configurada para la sucursal. Se omitirán las Notas de Crédito automáticas.");
            }
            else
            {
                foreach (var ret in returns)
                {
                    try
                    {
                        // Validar si ya existe una nota de crédito para esta devolución
                        var creditNoteExists = await _context.Invoices
                            .AnyAsync(i => i.ReturnId == ret.Id && i.Type == InvoiceType.CreditNote, cancellationToken);

                        if (creditNoteExists)
                        {
                            _logger.LogInformation("La devolución con ID {ReturnId} ya cuenta con una Nota de Crédito. Omitiendo...", ret.Id);
                            continue;
                        }

                        if (!ret.SaleId.HasValue)
                        {
                            _logger.LogWarning("La devolución con ID {ReturnId} no tiene una venta original asociada. Omitiendo nota de crédito...", ret.Id);
                            continue;
                        }

                        // Obtener la venta original
                        var origSale = await _context.Sales
                            .FirstOrDefaultAsync(s => s.Id == ret.SaleId.Value, cancellationToken);

                        if (origSale == null)
                        {
                            _logger.LogWarning("Venta original {SaleId} no encontrada para la devolución {ReturnId}.", ret.SaleId, ret.Id);
                            continue;
                        }

                        // Determinar el CFDI de Ingreso original (factura individual o la global recién creada)
                        Invoice? origInvoice = null;
                        if (origSale.IsInvoiced || origSale.InvoiceId != null)
                        {
                            if (Guid.TryParse(origSale.InvoiceId, out var origInvoiceGuid))
                            {
                                origInvoice = await _context.Invoices
                                    .FirstOrDefaultAsync(i => i.Id == origInvoiceGuid && i.Status == InvoiceStatus.Stamped, cancellationToken);
                            }
                        }

                        // Si no está facturada individualmente, se asocia automáticamente a la Factura Global recién generada
                        if (origInvoice == null)
                        {
                            origInvoice = globalInvoice;
                        }

                        if (origInvoice == null || string.IsNullOrEmpty(origInvoice.Uuid))
                        {
                            _logger.LogWarning("No se pudo resolver el CFDI de Ingreso (individual o global) para la venta {SaleNumber}. La devolución {ReturnId} se omitirá.", origSale.SaleNumber, ret.Id);
                            continue;
                        }

                        // Mapear forma de pago desde la venta original
                        string formaPagoNC = origSale.PaymentMethod switch
                        {
                            PaymentMethodType.Cash => "01",
                            PaymentMethodType.CreditCard => "04",
                            PaymentMethodType.DebitCard => "28",
                            PaymentMethodType.Transfer => "03",
                            PaymentMethodType.Check => "02",
                            _ => "01"
                        };

                        // Generar desglose de impuestos local para la nota de crédito
                        var returnTaxBreakdowns = ret.Items
                            .GroupBy(i => new { i.TaxRate, i.IsTaxExempt })
                            .Select(g => new TaxBreakdown(
                                Rate: g.Key.TaxRate,
                                BaseAmount: g.Sum(i => i.UnitPrice * i.Quantity),
                                TaxAmount: g.Key.IsTaxExempt ? 0 : g.Sum(i => (i.UnitPrice * i.Quantity) * (g.Key.TaxRate / 100m)),
                                IsExempt: g.Key.IsTaxExempt
                            )).ToList();

                        int nextNCFolioNum = creditNoteSequence.LastFolio + 1;
                        string ncFolioStr = nextNCFolioNum.ToString();
                        string ncSeriesStr = creditNoteSequence.Series;

                        var creditInvoice = Invoice.CreateCreditNote(
                            branchId: branchId,
                            series: ncSeriesStr,
                            folio: ncFolioStr,
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

                        // Generar XML CFDI 4.0 sin firmar para la nota de crédito
                        string unsignedNCXml = _cfdiXmlGenerator.GenerateCfdi40Xml(creditInvoice, config, "PUE", formaPagoNC);

                        // Generar Cadena Original
                        string cadenaOriginalNC = _cfdiXmlGenerator.GenerateCadenaOriginal(unsignedNCXml);

                        // Firmar usando CSD si está disponible o sello representativo
                        string selloNC = string.Empty;
                        if (config.CsdPrivateKeyData != null && !string.IsNullOrEmpty(config.CsdPassword))
                        {
                            selloNC = _csdCertificateService.SignCadenaOriginal(cadenaOriginalNC, config.CsdPrivateKeyData, config.CsdPassword);
                        }
                        else
                        {
                            selloNC = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes($"SELLO_NC_{creditInvoice.InvoiceNumber}_{DateTime.Now.Ticks}"));
                        }

                        // Insertar sello
                        var docNC = XDocument.Parse(unsignedNCXml);
                        docNC.Root?.SetAttributeValue("Sello", selloNC);
                        using var swNC = new Utf8StringWriter();
                        docNC.Save(swNC);
                        string signedNCXml = swNC.ToString();

                        _logger.LogInformation("Enviando timbrado de Nota de Crédito para Devolución {ReturnId} al PAC...", ret.Id);
                        var creditResult = await _pacService.StampXmlAsync(
                            signedNCXml,
                            pacUser,
                            pacKey,
                            pacUrl,
                            cancellationToken
                        );

                        if (creditResult != null && creditResult.Success && creditResult.Uuid != null)
                        {
                            creditInvoice.Stamp(
                                uuid: creditResult.Uuid,
                                stampedAt: creditResult.StampedAt ?? DateTime.Now,
                                selloDigitalEmisor: selloNC,
                                selloDigitalSAT: creditResult.SelloSAT ?? "",
                                noCertificadoEmisor: config.CsdSerialNumber ?? "00001000000500000000",
                                noCertificadoSAT: creditResult.CertificadoSAT ?? "",
                                cadenaOriginal: creditResult.CadenaOriginalTfd ?? ""
                            );

                            _context.Invoices.Add(creditInvoice);

                            // Actualizar secuencia de folios
                            creditNoteSequence.ResetTo(nextNCFolioNum);

                            // Registrar en el turno
                            shift.RegisterCreditNote(creditInvoice.Id.ToString(), ret.TotalRefund, ret.Reason);

                            await _context.SaveChangesAsync(cancellationToken);
                            _logger.LogInformation("Nota de Crédito {NCNumber} timbrada de forma automática.", creditInvoice.InvoiceNumber);
                        }
                        else
                        {
                            _logger.LogWarning("El timbrado de la Nota de Crédito automática para la devolución {ReturnId} falló: {ErrorMessage}", ret.Id, creditResult?.ErrorMessage);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error al generar Nota de Crédito automática para la devolución {ReturnId} de forma tolerante.", ret.Id);
                    }
                }
            }
        }

        return globalInvoice.Id;
    }

    private class Utf8StringWriter : StringWriter
    {
        public override System.Text.Encoding Encoding => System.Text.Encoding.UTF8;
    }
}
