using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PDV.Application.Common.Interfaces;
using PDV.Domain.Entities;
using PDV.Domain.Enums;
using PDV.Domain.Repositories;
using PDV.Domain.ValueObjects;
using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace PDV.Application.Features.Sales.Commands.CreateInvoice;

public record CreateInvoiceCommand : IRequest<Guid>
{
    public Guid SaleId { get; set; }
    public decimal TaxRate { get; set; } = 0.16m; // 16% IVA por defecto
    public bool IsGlobal { get; set; } = false;
    public Guid? ClientId { get; set; }
    public string UsoCfdi { get; set; } = "G03";
    public string MetodoPago { get; set; } = "PUE";
    public string FormaPago { get; set; } = "01";
    public string? ReceiverFiscalRegime { get; set; }
    public string? ReceiverZipCode { get; set; }
}

public class CreateInvoiceCommandValidator : AbstractValidator<CreateInvoiceCommand>
{
    public CreateInvoiceCommandValidator()
    {
        RuleFor(v => v.SaleId)
            .NotEmpty().WithMessage("El ID de venta es requerido");

        RuleFor(v => v.TaxRate)
            .GreaterThanOrEqualTo(0)
            .LessThanOrEqualTo(1)
            .WithMessage("La tasa de impuesto debe estar entre 0 y 1");

        RuleFor(v => v.UsoCfdi)
            .NotEmpty().WithMessage("El uso de CFDI es requerido");

        RuleFor(v => v.MetodoPago)
            .NotEmpty().WithMessage("El método de pago es requerido");

        RuleFor(v => v.FormaPago)
            .NotEmpty().WithMessage("La forma de pago es requerida");
    }
}

public class CreateInvoiceCommandHandler : IRequestHandler<CreateInvoiceCommand, Guid>
{
    private readonly ISaleRepository _saleRepository;
    private readonly IApplicationDbContext _context;
    private readonly ICsdCertificateService _csdCertificateService;
    private readonly ICfdiXmlGenerator _cfdiXmlGenerator;
    private readonly IPacService _pacService;
    private readonly IComercialApiSyncService _comercialSyncService;

    public CreateInvoiceCommandHandler(
        ISaleRepository saleRepository,
        IApplicationDbContext context,
        ICsdCertificateService csdCertificateService,
        ICfdiXmlGenerator cfdiXmlGenerator,
        IPacService pacService,
        IComercialApiSyncService comercialSyncService)
    {
        _saleRepository = saleRepository;
        _context = context;
        _csdCertificateService = csdCertificateService;
        _cfdiXmlGenerator = cfdiXmlGenerator;
        _pacService = pacService;
        _comercialSyncService = comercialSyncService;
    }

    public async Task<Guid> Handle(CreateInvoiceCommand request, CancellationToken cancellationToken)
    {
        var sale = await _saleRepository.GetByIdWithItemsAsync(request.SaleId, cancellationToken);
        
        if (sale == null)
        {
            throw new InvalidOperationException($"Venta con ID {request.SaleId} no encontrada");
        }

        // Validar que la venta esté pagada
        if (!sale.IsPaid)
        {
            throw new InvalidOperationException("No se puede crear factura de una venta que aún no ha sido pagada");
        }

        // Validar que no esté cancelada
        if (sale.IsCancelled)
        {
            throw new InvalidOperationException("No se puede crear factura de una venta cancelada");
        }

        var config = await _context.SystemConfigurations.FirstOrDefaultAsync(cancellationToken);
        if (config == null)
        {
            throw new InvalidOperationException("No se han configurado los parámetros fiscales del sistema.");
        }

        // Si la integración con CONTPAQi Comercial está activa, delegar timbrado y generación
        if (!string.IsNullOrWhiteSpace(config.ComercialApiUrl))
        {
            var folioSeq = await _context.FolioSequences
                .FirstOrDefaultAsync(fs => fs.BranchId == sale.BranchId && fs.SeriesType == (request.IsGlobal ? InvoiceType.Global : InvoiceType.Customer), cancellationToken);

            if (folioSeq == null)
            {
                throw new InvalidOperationException("No se ha configurado la secuencia de folios de facturación para esta sucursal.");
            }

            Guid? clientGuid = request.ClientId ?? sale.ClientId;
            if (!request.IsGlobal && clientGuid == null)
            {
                throw new InvalidOperationException("No se puede crear factura por cliente de una venta sin cliente asignado");
            }

            string clientCode = "PUBLICOGENERAL";
            string apiRfc = "XAXX010101000";
            string apiNombre = "PUBLICO EN GENERAL";
            string apiReceiverFiscalRegime = request.ReceiverFiscalRegime ?? "616";
            string apiReceiverZipCode = request.ReceiverZipCode ?? "00000";

            if (!request.IsGlobal && clientGuid.HasValue && clientGuid.Value != Guid.Empty)
            {
                var cliente = await _context.Clients.FindAsync(new object[] { clientGuid.Value }, cancellationToken);
                if (cliente != null)
                {
                    if (string.IsNullOrWhiteSpace(cliente.TaxId))
                    {
                        throw new InvalidOperationException($"El cliente '{cliente.Name}' no cuenta con RFC registrado. Por favor actualice sus datos fiscales antes de facturar.");
                    }

                    clientCode = cliente.Code;
                    apiRfc = cliente.TaxId;
                    apiNombre = cliente.Name;
                    
                    apiReceiverFiscalRegime = request.ReceiverFiscalRegime ?? cliente.FiscalRegime ?? "616";
                    apiReceiverZipCode = request.ReceiverZipCode ?? cliente.FiscalZipCode ?? "00000";

                    if (request.ReceiverFiscalRegime != null || request.ReceiverZipCode != null)
                    {
                        cliente.UpdateFiscalProfile(apiReceiverFiscalRegime, apiReceiverZipCode);
                        _context.Clients.Update(cliente);
                    }
                }
            }

            var apiPartidas = sale.Items.Select(item => new FacturaPartidaDto
            {
                CodigoProducto = item.Product?.Code ?? string.Empty,
                Unidades = (double)item.Quantity,
                PrecioUnitario = (double)item.UnitPrice,
                CodigoAlmacen = "1"
            }).ToList();

            var apiRequest = new GenerarFacturaComercialDto
            {
                CodigoConcepto = folioSeq.ConceptCode ?? "FCLI",
                Serie = folioSeq.Series,
                CodigoCliente = clientCode,
                Referencia = sale.SaleNumber.ToString(),
                CodigoAgente = string.Empty,
                NumeroMoneda = 1,
                TipoCambio = 1.0,
                UsoCfdi = request.UsoCfdi,
                MetodoPago = request.MetodoPago,
                FormaPago = request.FormaPago,
                AutoTimbrar = true,
                Partidas = apiPartidas
            };

            var apiResult = await _comercialSyncService.GenerarFacturaComercialAsync(apiRequest, cancellationToken);
            if (apiResult == null || !apiResult.Timbrado || apiResult.DatosFiscales == null)
            {
                throw new InvalidOperationException($"Error al generar factura vía API Comercial: {apiResult?.Mensaje ?? "Respuesta vacía"}");
            }

            var localTaxBreakdowns = sale.Items
                .GroupBy(i => new { i.TaxRate, i.IsTaxExempt })
                .Select(g => new TaxBreakdown(
                    Rate: g.Key.TaxRate,
                    BaseAmount: g.Sum(i => i.UnitPrice * i.Quantity),
                    TaxAmount: g.Key.IsTaxExempt ? 0 : g.Sum(i => (i.UnitPrice * i.Quantity) * (g.Key.TaxRate / 100m)),
                    IsExempt: g.Key.IsTaxExempt
                )).ToList();

            var localInvoice = Invoice.CreateCustomerInvoice(
                branchId: sale.BranchId,
                series: apiResult.Serie,
                folio: apiResult.Folio,
                saleId: sale.Id,
                clientId: clientGuid,
                receiverTaxId: apiRfc,
                receiverName: apiNombre,
                cfdiUsage: Enum.TryParse<CfdiUsage>(request.UsoCfdi, true, out var u) ? u : CfdiUsage.GeneralExpense,
                subtotal: sale.Items.Sum(i => i.Quantity * i.UnitPrice),
                taxBreakdowns: localTaxBreakdowns,
                receiverFiscalRegime: apiReceiverFiscalRegime,
                receiverZipCode: apiReceiverZipCode
            );

            localInvoice.Stamp(
                uuid: apiResult.DatosFiscales.UUID,
                stampedAt: DateTime.TryParse(apiResult.DatosFiscales.FechaTimbrado, out var dt) ? dt : DateTime.UtcNow,
                selloDigitalEmisor: apiResult.DatosFiscales.SelloDigitalEmisor,
                selloDigitalSAT: apiResult.DatosFiscales.SelloDigitalSAT,
                noCertificadoEmisor: apiResult.DatosFiscales.NoCertificadoEmisor,
                noCertificadoSAT: apiResult.DatosFiscales.NoCertificadoSAT,
                cadenaOriginal: apiResult.DatosFiscales.CadenaOriginal
            );

            _context.Invoices.Add(localInvoice);

            if (int.TryParse(apiResult.Folio, out var parsedFolio))
            {
                folioSeq.ResetTo(parsedFolio);
            }

            sale.MarkAsInvoiced(localInvoice.Id.ToString());

            await _context.SaveChangesAsync(cancellationToken);

            return localInvoice.Id;
        }

        string pacUrl = !string.IsNullOrEmpty(config.PacUrl) ? config.PacUrl : (!string.IsNullOrEmpty(config.ComercialApiUrl) ? config.ComercialApiUrl : "https://mock-pac.sat.gob.mx");
        string pacUser = !string.IsNullOrEmpty(config.PacApiUser) ? config.PacApiUser : "CONTPAQI_PAC";
        string pacKey = config.PacApiKey ?? config.ComercialApiKey ?? string.Empty;

        // Obtener la secuencia de folios de facturación
        var folioSequence = await _context.FolioSequences
            .FirstOrDefaultAsync(fs => fs.BranchId == sale.BranchId && fs.SeriesType == (request.IsGlobal ? InvoiceType.Global : InvoiceType.Customer), cancellationToken);

        if (folioSequence == null)
        {
            throw new InvalidOperationException("No se ha configurado la secuencia de folios de facturación para esta sucursal.");
        }

        // Si es factura por cliente, validar que tengamos un cliente seleccionado o asignado
        Guid? finalClientId = request.ClientId ?? sale.ClientId;
        if (!request.IsGlobal && finalClientId == null)
        {
            throw new InvalidOperationException("No se puede crear factura por cliente de una venta sin cliente asignado");
        }

        string rfc = "XAXX010101000";
        string nombre = "PUBLICO EN GENERAL";
        string receiverFiscalRegime = request.ReceiverFiscalRegime ?? "616";
        string receiverZipCode = request.ReceiverZipCode ?? "00000";
        var cfdiUsage = CfdiUsage.ToDefine;

        if (!request.IsGlobal && finalClientId.HasValue && finalClientId.Value != Guid.Empty)
        {
            var cliente = await _context.Clients.FindAsync(new object[] { finalClientId.Value }, cancellationToken);
            if (cliente != null)
            {
                if (string.IsNullOrWhiteSpace(cliente.TaxId))
                {
                    throw new InvalidOperationException($"El cliente '{cliente.Name}' no cuenta con RFC registrado. Por favor actualice sus datos fiscales antes de facturar.");
                }

                rfc = cliente.TaxId;
                nombre = cliente.Name;
                cfdiUsage = Enum.TryParse<CfdiUsage>(request.UsoCfdi, true, out var u) ? u : CfdiUsage.GeneralExpense;
                
                receiverFiscalRegime = request.ReceiverFiscalRegime ?? cliente.FiscalRegime ?? "616";
                receiverZipCode = request.ReceiverZipCode ?? cliente.FiscalZipCode ?? "00000";

                // Si cambiaron o se añadieron los valores fiscales, guardarlos en el perfil del cliente
                if (request.ReceiverFiscalRegime != null || request.ReceiverZipCode != null)
                {
                    cliente.UpdateFiscalProfile(
                        request.ReceiverFiscalRegime ?? cliente.FiscalRegime,
                        request.ReceiverZipCode ?? cliente.FiscalZipCode
                    );
                    _context.Clients.Update(cliente);
                }
            }
        }

        // Generar desglose de impuestos local
        var taxBreakdowns = sale.Items
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

        // Crear la factura en estado Draft
        var invoice = Invoice.CreateCustomerInvoice(
            branchId: sale.BranchId,
            series: seriesStr,
            folio: folioStr,
            saleId: sale.Id,
            clientId: finalClientId,
            receiverTaxId: rfc,
            receiverName: nombre,
            cfdiUsage: cfdiUsage,
            subtotal: sale.Items.Sum(i => i.Quantity * i.UnitPrice),
            taxBreakdowns: taxBreakdowns,
            receiverFiscalRegime: receiverFiscalRegime,
            receiverZipCode: receiverZipCode
        );

        // Generar XML CFDI 4.0 sin firmar
        string unsignedXml = _cfdiXmlGenerator.GenerateCfdi40Xml(invoice, config, request.MetodoPago, request.FormaPago);

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
            sello = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes($"SELLO_DIGITAL_{invoice.InvoiceNumber}_{DateTime.UtcNow.Ticks}"));
        }

        // Insertar sello en el XML
        var doc = XDocument.Parse(unsignedXml);
        doc.Root?.SetAttributeValue("Sello", sello);
        using var sw = new Utf8StringWriter();
        doc.Save(sw);
        string signedXml = sw.ToString();

        // Enviar XML al PAC para timbrado
        var stampResult = await _pacService.StampXmlAsync(
            signedXml,
            pacUser,
            pacKey,
            pacUrl,
            cancellationToken
        );

        if (!stampResult.Success || stampResult.Uuid == null)
        {
            throw new InvalidOperationException($"Error al timbrar la factura ante el PAC: {stampResult.ErrorMessage}");
        }

        // Estampar la factura con los datos fiscales recibidos del PAC
        invoice.Stamp(
            uuid: stampResult.Uuid,
            stampedAt: stampResult.StampedAt ?? DateTime.UtcNow,
            selloDigitalEmisor: sello,
            selloDigitalSAT: stampResult.SelloSAT ?? "",
            noCertificadoEmisor: config.CsdSerialNumber ?? "00001000000500000000",
            noCertificadoSAT: stampResult.CertificadoSAT ?? "",
            cadenaOriginal: stampResult.CadenaOriginalTfd ?? ""
        );

        // Guardar factura
        _context.Invoices.Add(invoice);

        // Actualizar secuencia de folios
        folioSequence.ResetTo(nextFolioNum);

        // Marcar la venta original como facturada
        sale.MarkAsInvoiced(invoice.Id.ToString());

        await _context.SaveChangesAsync(cancellationToken);

        return invoice.Id;
    }

    private class Utf8StringWriter : StringWriter
    {
        public override System.Text.Encoding Encoding => System.Text.Encoding.UTF8;
    }
}
