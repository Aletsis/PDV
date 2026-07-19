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

    public CreateInvoiceCommandHandler(
        ISaleRepository saleRepository,
        IApplicationDbContext context,
        ICsdCertificateService csdCertificateService,
        ICfdiXmlGenerator cfdiXmlGenerator,
        IPacService pacService)
    {
        _saleRepository = saleRepository;
        _context = context;
        _csdCertificateService = csdCertificateService;
        _cfdiXmlGenerator = cfdiXmlGenerator;
        _pacService = pacService;
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

        if (!config.IsCsdValid() || config.CsdCertificateData == null || config.CsdPrivateKeyData == null || string.IsNullOrEmpty(config.CsdPassword))
        {
            throw new InvalidOperationException("El Certificado de Sello Digital (CSD) del emisor no está configurado o ya ha expirado.");
        }

        if (string.IsNullOrEmpty(config.PacUrl) || string.IsNullOrEmpty(config.PacApiUser))
        {
            throw new InvalidOperationException("Las credenciales de acceso al PAC no están configuradas.");
        }

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

        // Firmar Cadena Original usando la llave privada CSD
        string sello = _csdCertificateService.SignCadenaOriginal(cadenaOriginal, config.CsdPrivateKeyData, config.CsdPassword);

        // Insertar sello en el XML
        var doc = XDocument.Parse(unsignedXml);
        doc.Root?.SetAttributeValue("Sello", sello);
        using var sw = new Utf8StringWriter();
        doc.Save(sw);
        string signedXml = sw.ToString();

        // Enviar XML al PAC para timbrado
        var stampResult = await _pacService.StampXmlAsync(
            signedXml,
            config.PacApiUser,
            config.PacApiKey ?? string.Empty,
            config.PacUrl ?? string.Empty,
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
            noCertificadoEmisor: config.CsdSerialNumber ?? "",
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
