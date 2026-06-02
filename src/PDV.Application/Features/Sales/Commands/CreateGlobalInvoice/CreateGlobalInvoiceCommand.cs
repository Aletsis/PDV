using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PDV.Application.Common.Interfaces;
using PDV.Domain.Entities;
using PDV.Domain.Enums;
using PDV.Domain.ValueObjects;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace PDV.Application.Features.Sales.Commands.CreateGlobalInvoice;

public record CreateGlobalInvoiceCommand(
    Guid ShiftId, 
    string CodigoProductoGravado = "01010101", 
    string CodigoProductoExento = "01010101"
) : IRequest<Guid>;

public class CreateGlobalInvoiceCommandHandler : IRequestHandler<CreateGlobalInvoiceCommand, Guid>
{
    private readonly IApplicationDbContext _context;
    private readonly IComercialApiSyncService _comercialApiSyncService;
    private readonly ILogger<CreateGlobalInvoiceCommandHandler> _logger;

    public CreateGlobalInvoiceCommandHandler(
        IApplicationDbContext context,
        IComercialApiSyncService comercialApiSyncService,
        ILogger<CreateGlobalInvoiceCommandHandler> logger)
    {
        _context = context;
        _comercialApiSyncService = comercialApiSyncService;
        _logger = logger;
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

        // 3. Recuperar secuencia de folios para facturas globales
        var folioSequence = await _context.FolioSequences
            .FirstOrDefaultAsync(fs => fs.BranchId == branchId && fs.SeriesType == InvoiceType.Global, cancellationToken);

        if (folioSequence == null || string.IsNullOrWhiteSpace(folioSequence.ConceptCode))
        {
            throw new InvalidOperationException("No se ha configurado la secuencia de folios ni el código de concepto de Factura Global para esta sucursal.");
        }

        // Obtener códigos de productos genéricos desde el request
        var codProdGravado = request.CodigoProductoGravado;
        var codProdExento = request.CodigoProductoExento;

        // 4. Mapear cada venta a ConceptoGlobalDto
        var conceptosDto = new List<ConceptoGlobalDto>();

        foreach (var sale in sales)
        {
            var trasladosDto = sale.Items
                .GroupBy(i => new { i.TaxRate, i.IsTaxExempt })
                .Select(g => new TrasladoConceptoDto
                {
                    Base = (double)g.Sum(i => i.UnitPrice * i.Quantity),
                    Impuesto = "002",
                    TipoFactor = g.Key.IsTaxExempt ? "Exento" : "Tasa",
                    TasaOCuota = g.Key.IsTaxExempt ? "0.000000" : (g.Key.TaxRate / 100m).ToString("F6", CultureInfo.InvariantCulture),
                    Importe = g.Key.IsTaxExempt ? 0 : (double)g.Sum(i => (i.UnitPrice * i.Quantity) * (g.Key.TaxRate / 100m))
                }).ToList();

            conceptosDto.Add(new ConceptoGlobalDto
            {
                NoIdentificacion = sale.SaleNumber,
                ValorUnitario = (double)sale.Subtotal,
                Importe = (double)sale.Subtotal,
                Traslados = trasladosDto
            });
        }

        // 5. Construir y enviar petición al API Comercial centralizado
        var globalCommand = new CreateFacturaGlobalCommandDto
        {
            CodigoConcepto = folioSequence.ConceptCode,
            Serie = folioSequence.Series,
            CodigoClientePublicoGeneral = "PUBLICOGENERAL",
            Periodicidad = "01", // Diario
            Meses = DateTime.Now.Month.ToString("D2"),
            Anio = DateTime.Now.Year.ToString(),
            UsoCfdi = "S01", // Sin efectos fiscales
            MetodoPago = "PUE",
            FormaPago = "01", // Efectivo por defecto en global
            CodigoProductoGravado = codProdGravado,
            CodigoProductoExento = codProdExento,
            CsdPassword = string.Empty, // El servidor usará su configuración CsdPassword en ServerManager
            AutoTimbrar = true,
            CodigoAlmacen = "1",
            Conceptos = conceptosDto
        };

        _logger.LogInformation("Enviando timbrado de Factura Global para Turno {ShiftId} al servidor central...", shift.Id);
        var apiResult = await _comercialApiSyncService.GenerarFacturaGlobalComercialAsync(globalCommand, cancellationToken);

        if (apiResult == null || !apiResult.Timbrado || apiResult.DatosFiscales == null)
        {
            throw new InvalidOperationException($"No se pudo generar ni timbrar la Factura Global: {apiResult?.Mensaje ?? "Error desconocido en el servidor central"}");
        }

        // 6. Crear la Factura Global en nuestra base de datos local
        var taxBreakdowns = sales.SelectMany(s => s.Items)
            .GroupBy(i => new { i.TaxRate, i.IsTaxExempt })
            .Select(g => new TaxBreakdown(
                Rate: g.Key.TaxRate,
                BaseAmount: g.Sum(i => i.UnitPrice * i.Quantity),
                TaxAmount: g.Key.IsTaxExempt ? 0 : g.Sum(i => (i.UnitPrice * i.Quantity) * (g.Key.TaxRate / 100m)),
                IsExempt: g.Key.IsTaxExempt
            )).ToList();

        var globalInvoice = Invoice.CreateGlobalInvoice(
            branchId: branchId,
            series: apiResult.Serie,
            folio: apiResult.Folio,
            shiftId: shift.Id,
            subtotal: sales.Sum(s => s.Subtotal),
            taxBreakdowns: taxBreakdowns
        );

        DateTime fechaTimbrado = DateTime.UtcNow;
        if (DateTime.TryParse(apiResult.DatosFiscales.FechaTimbrado, out var ft))
        {
            fechaTimbrado = ft.ToUniversalTime();
        }

        globalInvoice.Stamp(
            uuid: apiResult.DatosFiscales.UUID,
            stampedAt: fechaTimbrado,
            selloDigitalEmisor: apiResult.DatosFiscales.SelloDigitalEmisor,
            selloDigitalSAT: apiResult.DatosFiscales.SelloDigitalSAT,
            noCertificadoEmisor: apiResult.DatosFiscales.NoCertificadoEmisor,
            noCertificadoSAT: apiResult.DatosFiscales.NoCertificadoSAT,
            cadenaOriginal: apiResult.DatosFiscales.CadenaOriginal
        );

        _context.Invoices.Add(globalInvoice);

        // Actualizar atómicamente la secuencia de folios
        if (int.TryParse(apiResult.Folio, out var newFolio))
        {
            folioSequence.ResetTo(newFolio);
        }

        // Registrar en el turno y en las ventas asociadas
        shift.MarkAsGlobalInvoiced(globalInvoice.Id.ToString());

        foreach (var sale in sales)
        {
            sale.MarkAsInvoiced(globalInvoice.Id.ToString());
        }

        await _context.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Factura Global {InvoiceNum} timbrada e integrada exitosamente.", globalInvoice.InvoiceNumber);

        // 7. PROCESAMIENTO AUTOMÁTICO DE NOTAS DE CRÉDITO DE DEVOLUCIONES
        var returns = await _context.Returns
            .Include(r => r.Items)
            .Where(r => r.ShiftId == shift.Id && r.IsCompleted)
            .ToListAsync(cancellationToken);

        if (returns.Any())
        {
            _logger.LogInformation("Procesando {Count} devoluciones del turno para generar Notas de Crédito de forma automática...", returns.Count);

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

                    // Obtener secuencia de folios para Notas de Crédito
                    var creditNoteSequence = await _context.FolioSequences
                        .FirstOrDefaultAsync(fs => fs.BranchId == branchId && fs.SeriesType == InvoiceType.CreditNote, cancellationToken);

                    if (creditNoteSequence == null || string.IsNullOrWhiteSpace(creditNoteSequence.ConceptCode))
                    {
                        _logger.LogWarning("Secuencia de folios para Nota de Crédito no configurada para la sucursal. Se omite esta devolución.");
                        continue;
                    }

                    // Obtener cliente de la venta o por defecto
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

                    // Mapear partidas devueltas
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

                    // Mapeo Forma de Pago desde la venta original
                    string formaPago = origSale.PaymentMethod switch
                    {
                        PaymentMethodType.Cash => "01",
                        PaymentMethodType.CreditCard => "04",
                        PaymentMethodType.DebitCard => "28",
                        PaymentMethodType.Transfer => "03",
                        PaymentMethodType.Check => "02",
                        _ => "01"
                    };

                    // Enviar timbrado de egreso al API de Comercial
                    var apiCreditCommand = new GenerarFacturaComercialDto
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

                    _logger.LogInformation("Enviando timbrado de Nota de Crédito para Devolución {ReturnId} (UUID relacionado: {Uuid}) al servidor central...", ret.Id, origInvoice.Uuid);
                    var creditResult = await _comercialApiSyncService.GenerarFacturaComercialAsync(apiCreditCommand, cancellationToken);

                    if (creditResult != null && creditResult.Timbrado && creditResult.DatosFiscales != null)
                    {
                        // Generar desglose de impuestos local para la nota de crédito
                        var returnTaxBreakdowns = ret.Items
                            .GroupBy(i => new { i.TaxRate, i.IsTaxExempt })
                            .Select(g => new TaxBreakdown(
                                Rate: g.Key.TaxRate,
                                BaseAmount: g.Sum(i => i.UnitPrice * i.Quantity),
                                TaxAmount: g.Key.IsTaxExempt ? 0 : g.Sum(i => (i.UnitPrice * i.Quantity) * (g.Key.TaxRate / 100m)),
                                IsExempt: g.Key.IsTaxExempt
                            )).ToList();

                        var creditInvoice = Invoice.CreateCreditNote(
                            branchId: branchId,
                            series: creditResult.Serie,
                            folio: creditResult.Folio,
                            returnId: ret.Id,
                            clientId: origSale.ClientId ?? Guid.Empty,
                            receiverTaxId: origInvoice.ReceiverTaxId,
                            receiverName: origInvoice.ReceiverName,
                            relatedUuid: origInvoice.Uuid,
                            subtotal: ret.Subtotal,
                            taxBreakdowns: returnTaxBreakdowns
                        );

                        DateTime ftCredit = DateTime.UtcNow;
                        if (DateTime.TryParse(creditResult.DatosFiscales.FechaTimbrado, out var ftc))
                        {
                            ftCredit = ftc.ToUniversalTime();
                        }

                        creditInvoice.Stamp(
                            uuid: creditResult.DatosFiscales.UUID,
                            stampedAt: ftCredit,
                            selloDigitalEmisor: creditResult.DatosFiscales.SelloDigitalEmisor,
                            selloDigitalSAT: creditResult.DatosFiscales.SelloDigitalSAT,
                            noCertificadoEmisor: creditResult.DatosFiscales.NoCertificadoEmisor,
                            noCertificadoSAT: creditResult.DatosFiscales.NoCertificadoSAT,
                            cadenaOriginal: creditResult.DatosFiscales.CadenaOriginal
                        );

                        _context.Invoices.Add(creditInvoice);

                        // Actualizar secuencia de folios
                        if (int.TryParse(creditResult.Folio, out var newCreditFolio))
                        {
                            creditNoteSequence.ResetTo(newCreditFolio);
                        }

                        // Registrar en el turno
                        shift.RegisterCreditNote(creditInvoice.Id.ToString(), ret.TotalRefund, ret.Reason);

                        await _context.SaveChangesAsync(cancellationToken);
                        _logger.LogInformation("Nota de Crédito {NCNumber} timbrada e integrada de forma automática para la Devolución {ReturnId}.", creditInvoice.InvoiceNumber, ret.Id);
                    }
                    else
                    {
                        _logger.LogWarning("El timbrado de la Nota de Crédito automática para la devolución {ReturnId} falló: {Mensaje}", ret.Id, creditResult?.Mensaje);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error al generar Nota de Crédito automática para la devolución {ReturnId} de forma tolerante.", ret.Id);
                }
            }
        }

        return globalInvoice.Id;
    }
}
