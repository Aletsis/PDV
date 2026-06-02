using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PDV.Application.Common.Interfaces;
using PDV.Domain.Entities;
using PDV.Domain.Enums;
using PDV.Domain.Repositories;
using PDV.Domain.ValueObjects;

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
    private readonly IComercialApiSyncService _comercialApiSyncService;

    public CreateInvoiceCommandHandler(
        ISaleRepository saleRepository,
        IApplicationDbContext context,
        IComercialApiSyncService comercialApiSyncService)
    {
        _saleRepository = saleRepository;
        _context = context;
        _comercialApiSyncService = comercialApiSyncService;
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

        // Obtener la secuencia de folios para determinar el código de concepto en CONTPAQi
        var folioSequence = await _context.FolioSequences
            .FirstOrDefaultAsync(fs => fs.BranchId == sale.BranchId && fs.SeriesType == (request.IsGlobal ? InvoiceType.Global : InvoiceType.Customer), cancellationToken);

        if (folioSequence == null || string.IsNullOrWhiteSpace(folioSequence.ConceptCode))
        {
            throw new InvalidOperationException("No se ha configurado la secuencia de folios y el código de concepto de facturación para esta sucursal.");
        }

        // Si es factura por cliente, validar que tengamos un cliente seleccionado o asignado
        Guid? finalClientId = request.ClientId ?? sale.ClientId;
        if (!request.IsGlobal && finalClientId == null)
        {
            throw new InvalidOperationException("No se puede crear factura por cliente de una venta sin cliente asignado");
        }

        string rfc = "XAXX010101000";
        string nombre = "PUBLICO EN GENERAL";
        string codigoCliente = "PUBLICOGENERAL";
        var cfdiUsage = CfdiUsage.ToDefine;

        if (!request.IsGlobal && finalClientId.HasValue && finalClientId.Value != Guid.Empty)
        {
            var cliente = await _context.Clients.FindAsync(new object[] { finalClientId.Value }, cancellationToken);
            if (cliente != null)
            {
                rfc = cliente.TaxId;
                nombre = cliente.Name;
                codigoCliente = cliente.Code;
                cfdiUsage = Enum.TryParse<CfdiUsage>(request.UsoCfdi, true, out var u) ? u : CfdiUsage.GeneralExpense;
            }
        }

        // Obtener códigos de producto en Comercial
        var productIds = sale.Items.Select(i => i.ProductId).Distinct().ToList();
        var products = await _context.Products
            .Where(p => productIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, p => p, cancellationToken);

        // Mapear partidas
        var partidasDto = sale.Items.Select(item => new FacturaPartidaDto
        {
            CodigoProducto = products.TryGetValue(item.ProductId, out var product) ? product.Code : string.Empty,
            Unidades = (double)item.Quantity,
            PrecioUnitario = (double)item.UnitPrice,
            CodigoAlmacen = "1"
        }).ToList();

        // Enviar petición de timbrado a la API Comercial
        var apiCommand = new GenerarFacturaComercialDto
        {
            CodigoConcepto = folioSequence.ConceptCode,
            Serie = folioSequence.Series,
            CodigoCliente = codigoCliente,
            Referencia = $"Venta POS {sale.SaleNumber}",
            NumeroMoneda = 1,
            TipoCambio = 1.0,
            UsoCfdi = request.UsoCfdi,
            MetodoPago = request.MetodoPago,
            FormaPago = request.FormaPago,
            CsdPassword = string.Empty, // El servidor usará la contraseña configurada en ServerManager
            AutoTimbrar = true,
            Partidas = partidasDto
        };

        var apiResult = await _comercialApiSyncService.GenerarFacturaComercialAsync(apiCommand, cancellationToken);
        if (apiResult == null || !apiResult.Timbrado || apiResult.DatosFiscales == null)
        {
            throw new InvalidOperationException($"No se pudo timbrar la factura ante el PAC: {apiResult?.Mensaje ?? "Error desconocido en el servidor"}");
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

        // Crear la factura usando constructor de dominio con la Serie y Folio reales del SAT/CONTPAQi
        var invoice = Invoice.CreateCustomerInvoice(
            branchId: sale.BranchId,
            series: apiResult.Serie,
            folio: apiResult.Folio,
            saleId: sale.Id,
            clientId: finalClientId,
            receiverTaxId: rfc,
            receiverName: nombre,
            cfdiUsage: cfdiUsage,
            subtotal: sale.Items.Sum(i => i.Quantity * i.UnitPrice),
            taxBreakdowns: taxBreakdowns
        );

        // Registrar sello digital y datos del SAT
        DateTime fechaTimbrado = DateTime.UtcNow;
        if (DateTime.TryParse(apiResult.DatosFiscales.FechaTimbrado, out var ft))
        {
            fechaTimbrado = ft.ToUniversalTime();
        }

        invoice.Stamp(
            uuid: apiResult.DatosFiscales.UUID,
            stampedAt: fechaTimbrado,
            selloDigitalEmisor: apiResult.DatosFiscales.SelloDigitalEmisor,
            selloDigitalSAT: apiResult.DatosFiscales.SelloDigitalSAT,
            noCertificadoEmisor: apiResult.DatosFiscales.NoCertificadoEmisor,
            noCertificadoSAT: apiResult.DatosFiscales.NoCertificadoSAT,
            cadenaOriginal: apiResult.DatosFiscales.CadenaOriginal
        );

        // Guardar factura
        _context.Invoices.Add(invoice);

        // Actualizar atómicamente la secuencia de folios
        if (int.TryParse(apiResult.Folio, out var newFolio))
        {
            folioSequence.ResetTo(newFolio);
        }

        await _context.SaveChangesAsync(cancellationToken);

        return invoice.Id;
    }
}
