using Microsoft.EntityFrameworkCore;
using PDV.Application.Common.Interfaces;
using PDV.Domain.Entities;
using PDV.Domain.Enums;
using PDV.Domain.ValueObjects;
using PDV.Infrastructure.Persistence;
using System.Text;

namespace PDV.Infrastructure.Printing;

public class TicketGenerator : ITicketGenerator
{
    private readonly AppDbContext _context;

    public TicketGenerator(AppDbContext context)
    {
        _context = context;
    }

    // ──────────────────────────────────────────────────────────────────────────
    // 1. TICKET DE VENTA (Ventas sin factura / Público General)
    // ──────────────────────────────────────────────────────────────────────────
    public async Task<string> GenerateSaleTicketAsync(Guid saleId, CancellationToken cancellationToken = default)
    {
        var sale = await _context.Sales
            .Include(s => s.Items)
            .Include(s => s.Client)
            .Include(s => s.Branch)
                .ThenInclude(b => b!.Address)
            .Include(s => s.CashRegister)
            .FirstOrDefaultAsync(s => s.Id == saleId, cancellationToken)
            ?? throw new KeyNotFoundException($"Venta {saleId} no encontrada");

        var config = await _context.SystemConfigurations.FirstOrDefaultAsync(cancellationToken);
        var width = config?.TicketWidth ?? 48;

        var sb = new StringBuilder();

        // 1. Encabezado configurable o default
        AppendHeader(sb, config, sale.Branch, width);

        // 2. Título de comprobante
        sb.AppendLine(Center("TICKET DE VENTA", width));
        sb.AppendLine(DrawSeparator('=', width));
        sb.AppendLine();

        // 3. Información de la Venta
        sb.AppendLine($"Folio: {sale.SaleNumber}");
        sb.AppendLine($"Fecha: {sale.Date.ToLocalTime():dd/MM/yyyy HH:mm}");
        if (sale.CashRegister != null)
        {
            sb.AppendLine($"Caja: {sale.CashRegister.Name}");
        }
        if (!string.IsNullOrEmpty(sale.UserId))
        {
            sb.AppendLine($"Cajero: {sale.UserId}");
        }

        // Cliente
        if (sale.Client != null)
        {
            sb.AppendLine($"Cliente: {sale.Client.Name}");
            if (!string.IsNullOrEmpty(sale.Client.TaxId))
            {
                sb.AppendLine($"RFC: {sale.Client.TaxId.ToUpperInvariant()}");
            }
        }
        else
        {
            sb.AppendLine("Cliente: Público General");
        }

        if (sale.IsInvoiced)
        {
            sb.AppendLine("CFDI: Venta Facturada");
            if (!string.IsNullOrEmpty(sale.InvoiceId))
            {
                sb.AppendLine($"ID Factura: {sale.InvoiceId.Substring(0, Math.Min(8, sale.InvoiceId.Length))}...");
            }
        }

        sb.AppendLine(DrawSeparator('-', width));

        // 4. Tabla de Artículos
        // Calcular anchos dinámicos de columna: Prod (45%), Cant (12%), Precio (21%), Total (remaining)
        int nameW = (int)(width * 0.45);
        int qtyW = (int)(width * 0.12);
        int priceW = (int)(width * 0.21);
        int totalW = width - nameW - qtyW - priceW;

        sb.AppendLine(FormatTableRow(width,
            ("PRODUCTO", nameW, false),
            ("CANT", qtyW, true),
            ("PRECIO", priceW, true),
            ("TOTAL", totalW, true)
        ));
        sb.AppendLine(DrawSeparator('-', width));

        foreach (var item in sale.Items)
        {
            sb.AppendLine(FormatTableRow(width,
                (item.ProductName, nameW, false),
                (item.Quantity.ToString("0.##"), qtyW, true),
                (item.UnitPrice.ToString("C2"), priceW, true),
                (item.TotalAmount.ToString("C2"), totalW, true)
            ));
        }

        sb.AppendLine(DrawSeparator('=', width));

        // 5. Bloque de Totales
        sb.AppendLine(FormatTableRow(width, ("Subtotal:", width - totalW, true), (sale.Subtotal.ToString("C2"), totalW, true)));
        
        // Impuestos desglosados
        foreach (var tax in sale.Taxes)
        {
            var rateText = tax.IsExempt ? "Exento" : $"{tax.Rate:0.##}%";
            sb.AppendLine(FormatTableRow(width, ($"IVA {rateText}:", width - totalW, true), (tax.TaxAmount.ToString("C2"), totalW, true)));
        }

        sb.AppendLine(FormatTableRow(width, ("TOTAL:", width - totalW, true), (sale.TotalAmount.ToString("C2"), totalW, true)));
        sb.AppendLine(FormatTableRow(width, ("Método de Pago:", width - totalW, true), (GetPaymentMethodTranslation(sale.PaymentMethod), totalW, true)));
        sb.AppendLine();

        // 6. Pie de Página configurable o default
        AppendFooter(sb, config, width);

        // Comando de corte de papel parcial ESC/POS
        sb.Append("\x1B\x69");

        return sb.ToString();
    }

    // ──────────────────────────────────────────────────────────────────────────
    // 2. FACTURA ELECTRÓNICA (CFDI)
    // ──────────────────────────────────────────────────────────────────────────
    public async Task<string> GenerateInvoiceTicketAsync(Guid invoiceId, CancellationToken cancellationToken = default)
    {
        var invoice = await _context.Invoices
            .Include(i => i.Branch)
                .ThenInclude(b => b!.Address)
            .Include(i => i.Client)
            .Include(i => i.Sale)
                .ThenInclude(s => s!.Items)
            .Include(i => i.Sale)
                .ThenInclude(s => s!.Branch)
            .Include(i => i.Sale)
                .ThenInclude(s => s!.CashRegister)
            .FirstOrDefaultAsync(i => i.Id == invoiceId, cancellationToken)
            ?? throw new KeyNotFoundException($"Factura {invoiceId} no encontrada");

        var config = await _context.SystemConfigurations.FirstOrDefaultAsync(cancellationToken);
        var width = config?.TicketWidth ?? 48;

        var sb = new StringBuilder();

        // 1. Encabezado
        var branch = invoice.Branch ?? invoice.Sale?.Branch;
        AppendHeader(sb, config, branch, width);

        // 2. Título de comprobante fiscal
        sb.AppendLine(Center("FACTURA ELECTRÓNICA", width));
        var typeText = invoice.Type switch
        {
            InvoiceType.Customer => "CFDI DE INGRESO (CLIENTE)",
            InvoiceType.Global => "CFDI DE INGRESO (GLOBAL)",
            InvoiceType.CreditNote => "CFDI DE EGRESO (NOTA DE CRÉDITO)",
            _ => "CFDI"
        };
        sb.AppendLine(Center(typeText, width));
        sb.AppendLine(DrawSeparator('=', width));
        sb.AppendLine();

        // 3. Datos del Emisor y Certificado
        if (config != null)
        {
            sb.AppendLine("DATOS DEL EMISOR:");
            sb.AppendLine($"Régimen Fiscal: {config.FiscalRegime}");
            if (!string.IsNullOrEmpty(config.CsdSerialNumber))
            {
                sb.AppendLine($"No. Certificado: {config.CsdSerialNumber}");
            }
            sb.AppendLine(DrawSeparator('-', width));
        }

        // 4. Datos del Receptor
        sb.AppendLine("DATOS DEL RECEPTOR:");
        sb.AppendLine($"Nombre/Razón Social:");
        sb.AppendLine(invoice.ReceiverName.ToUpperInvariant());
        sb.AppendLine($"RFC: {invoice.ReceiverTaxId.ToUpperInvariant()}");
        sb.AppendLine($"Uso CFDI: {GetCfdiUsageDescription(invoice.CfdiUsage)}");
        sb.AppendLine(DrawSeparator('-', width));

        // 5. Datos de Identificación del Comprobante
        sb.AppendLine($"Serie/Folio: {invoice.InvoiceNumber}");
        sb.AppendLine($"Fecha Emisión: {invoice.InvoiceDate.ToLocalTime():dd/MM/yyyy HH:mm}");
        if (invoice.Sale != null)
        {
            sb.AppendLine($"Nota de Origen: {invoice.Sale.SaleNumber}");
        }

        // 6. Detalle de Conceptos / Artículos
        int nameW = (int)(width * 0.45);
        int qtyW = (int)(width * 0.12);
        int priceW = (int)(width * 0.21);
        int totalW = width - nameW - qtyW - priceW;

        sb.AppendLine(FormatTableRow(width,
            ("CONCEPTO/PROD", nameW, false),
            ("CANT", qtyW, true),
            ("PRECIO", priceW, true),
            ("TOTAL", totalW, true)
        ));
        sb.AppendLine(DrawSeparator('-', width));

        if (invoice.Sale != null && invoice.Sale.Items.Any())
        {
            foreach (var item in invoice.Sale.Items)
            {
                sb.AppendLine(FormatTableRow(width,
                    (item.ProductName, nameW, false),
                    (item.Quantity.ToString("0.##"), qtyW, true),
                    (item.UnitPrice.ToString("C2"), priceW, true),
                    (item.TotalAmount.ToString("C2"), totalW, true)
                ));
            }
        }
        else
        {
            // Facturas globales consolidadas o sin desglose manual
            sb.AppendLine(FormatTableRow(width,
                ("CONSOLIDADO GLOBAL DE VENTAS", nameW + qtyW, false),
                ("", priceW, true),
                (invoice.Subtotal.ToString("C2"), totalW, true)
            ));
        }

        sb.AppendLine(DrawSeparator('=', width));

        // 7. Totales
        sb.AppendLine($"{"Subtotal:".PadLeft(30)} {invoice.Subtotal.ToString("C2").PadLeft(10)}");
        sb.AppendLine($"{"IVA:".PadLeft(30)} {invoice.Tax.ToString("C2").PadLeft(10)}");
        sb.AppendLine($"{"TOTAL:".PadLeft(30)} {invoice.Total.ToString("C2").PadLeft(10)}");
        
        // Si la factura está timbrada, imprimir datos fiscales
        if (invoice.Status == InvoiceStatus.Stamped && !string.IsNullOrEmpty(invoice.Uuid))
        {
            sb.AppendLine();
            sb.AppendLine("================================================");
            sb.AppendLine(Center("DATOS FISCALES DEL CFDI", 48));
            sb.AppendLine("================================================");
            sb.AppendLine($"Folio Fiscal (UUID):");
            sb.AppendLine($" {invoice.Uuid}");
            sb.AppendLine($"No. Certificado SAT: {invoice.NoCertificadoSAT}");
            sb.AppendLine($"No. Certificado Emisor: {invoice.NoCertificadoEmisor}");
            sb.AppendLine($"Fecha Certificación: {invoice.StampedAt:dd/MM/yyyy HH:mm:ss}");
            sb.AppendLine();
            
            sb.AppendLine("Sello Digital del Emisor:");
            sb.AppendLine(WrapText(invoice.SelloDigitalEmisor ?? string.Empty, 48));
            sb.AppendLine();
            
            sb.AppendLine("Sello Digital del SAT:");
            sb.AppendLine(WrapText(invoice.SelloDigitalSAT ?? string.Empty, 48));
            sb.AppendLine();
            
            sb.AppendLine("Cadena Original SAT:");
            sb.AppendLine(WrapText(invoice.CadenaOriginal ?? string.Empty, 48));
            sb.AppendLine();
            sb.AppendLine("------------------------------------------------");
            sb.AppendLine(Center("Este documento es una representación", 48));
            sb.AppendLine(Center("impresa de un CFDI", 48));
            sb.AppendLine("------------------------------------------------");
        }

        sb.AppendLine();
        sb.AppendLine(Center("¡GRACIAS POR SU COMPRA!", 48));
        sb.AppendLine();
        sb.AppendLine();
        sb.AppendLine();

        // Comando de corte de papel ESC/POS
        sb.Append("\x1B\x69"); // Corte parcial

        return sb.ToString();
    }

    // ──────────────────────────────────────────────────────────────────────────
    // 3. TICKET DE DEVOLUCIÓN
    // ──────────────────────────────────────────────────────────────────────────
    public async Task<string> GenerateReturnTicketAsync(Guid returnId, CancellationToken cancellationToken = default)
    {
        var returnSale = await _context.Returns
            .Include(r => r.Items)
            .Include(r => r.Client)
            .Include(r => r.Branch)
                .ThenInclude(b => b!.Address)
            .Include(r => r.CashRegister)
            .Include(r => r.Sale)
            .FirstOrDefaultAsync(r => r.Id == returnId, cancellationToken)
            ?? throw new KeyNotFoundException($"Devolución {returnId} no encontrada");

        var config = await _context.SystemConfigurations.FirstOrDefaultAsync(cancellationToken);
        var width = config?.TicketWidth ?? 48;

        var sb = new StringBuilder();

        // 1. Encabezado
        AppendHeader(sb, config, returnSale.Branch, width);

        // 2. Título de comprobante
        sb.AppendLine(Center("TICKET DE DEVOLUCIÓN", width));
        sb.AppendLine(DrawSeparator('=', width));
        sb.AppendLine();

        // 3. Detalles de la Devolución
        var folioText = string.IsNullOrEmpty(returnSale.Series) ? returnSale.Folio.ToString() : $"{returnSale.Series}{returnSale.Folio}";
        sb.AppendLine($"Folio Devolución: {folioText}");
        sb.AppendLine($"Fecha: {returnSale.ReturnDate.ToLocalTime():dd/MM/yyyy HH:mm}");
        if (returnSale.Sale != null)
        {
            sb.AppendLine($"Ticket Original: {returnSale.Sale.SaleNumber}");
        }
        if (returnSale.CashRegister != null)
        {
            sb.AppendLine($"Caja: {returnSale.CashRegister.Name}");
        }
        if (!string.IsNullOrEmpty(returnSale.UserId))
        {
            sb.AppendLine($"Cajero: {returnSale.UserId}");
        }

        // Cliente
        if (returnSale.Client != null)
        {
            sb.AppendLine($"Cliente: {returnSale.Client.Name}");
        }
        else
        {
            sb.AppendLine("Cliente: Público General");
        }

        sb.AppendLine($"Motivo: {returnSale.Reason}");
        sb.AppendLine(DrawSeparator('-', width));

        // 4. Tabla de Artículos Devueltos
        int nameW = (int)(width * 0.45);
        int qtyW = (int)(width * 0.12);
        int priceW = (int)(width * 0.21);
        int totalW = width - nameW - qtyW - priceW;

        sb.AppendLine(FormatTableRow(width,
            ("PRODUCTO", nameW, false),
            ("CANT", qtyW, true),
            ("PRECIO", priceW, true),
            ("TOTAL", totalW, true)
        ));
        sb.AppendLine(DrawSeparator('-', width));

        foreach (var item in returnSale.Items)
        {
            sb.AppendLine(FormatTableRow(width,
                (item.ProductName, nameW, false),
                (item.Quantity.ToString("0.##"), qtyW, true),
                (item.UnitPrice.ToString("C2"), priceW, true),
                (item.TotalAmount.ToString("C2"), totalW, true)
            ));
        }

        sb.AppendLine(DrawSeparator('=', width));

        // 5. Totales de Devolución
        sb.AppendLine(FormatTableRow(width, ("Subtotal Devuelto:", width - totalW, true), (returnSale.Subtotal.ToString("C2"), totalW, true)));
        
        foreach (var tax in returnSale.Taxes)
        {
            var rateText = tax.IsExempt ? "Exento" : $"{tax.Rate:0.##}%";
            sb.AppendLine(FormatTableRow(width, ($"IVA Devuelto {rateText}:", width - totalW, true), (tax.TaxAmount.ToString("C2"), totalW, true)));
        }

        sb.AppendLine(FormatTableRow(width, ("TOTAL REEMBOLSO:", width - totalW, true), (returnSale.TotalRefund.ToString("C2"), totalW, true)));
        sb.AppendLine(FormatTableRow(width, ("Forma Reembolso:", width - totalW, true), (GetRefundMethodTranslation(returnSale.RefundMethod), totalW, true)));
        sb.AppendLine();

        // 6. Firmas
        sb.AppendLine();
        sb.AppendLine(Center("_________________________", width));
        sb.AppendLine(Center("Firma de Conformidad Cliente", width));
        sb.AppendLine();
        sb.AppendLine();

        AppendFooter(sb, config, width);

        // Corte parcial
        sb.Append("\x1B\x69");

        return sb.ToString();
    }

    // ──────────────────────────────────────────────────────────────────────────
    // 4. RECOLECCIONES DE EFECTIVO / ENTREGAS DE MORRALLA
    // ──────────────────────────────────────────────────────────────────────────
    public async Task<string> GenerateCashCollectionTicketAsync(Guid collectionId, CancellationToken cancellationToken = default)
    {
        var collection = await _context.CashCollections
            .Include(c => c.CashRegister)
                .ThenInclude(r => r!.Branch)
                    .ThenInclude(b => b!.Address)
            .Include(c => c.Employee)
            .FirstOrDefaultAsync(c => c.Id == collectionId, cancellationToken)
            ?? throw new KeyNotFoundException($"Movimiento de caja {collectionId} no encontrado");

        var config = await _context.SystemConfigurations.FirstOrDefaultAsync(cancellationToken);
        var width = config?.TicketWidth ?? 48;

        var sb = new StringBuilder();

        // 1. Encabezado
        AppendHeader(sb, config, collection.CashRegister?.Branch, width);

        // 2. Determinar si es Morralla (Inflow) o Retiro (Outflow)
        bool isInflow = collection.Reason.StartsWith("[INFLOW]", StringComparison.OrdinalIgnoreCase);
        var ticketTitle = isInflow ? "DOTACIÓN DE MORRALLA" : "RECOLECCIÓN DE EFECTIVO";
        var cleanReason = collection.Reason
            .Replace("[INFLOW]", "", StringComparison.OrdinalIgnoreCase)
            .Replace("[OUTFLOW]", "", StringComparison.OrdinalIgnoreCase)
            .Trim();

        sb.AppendLine(Center(ticketTitle, width));
        sb.AppendLine(DrawSeparator('=', width));
        sb.AppendLine();

        // 3. Detalles de la transacción
        sb.AppendLine($"Folio: {collection.Id.ToString().Substring(0, 8).ToUpperInvariant()}");
        sb.AppendLine($"Fecha: {collection.CollectionDate.ToLocalTime():dd/MM/yyyy HH:mm}");
        if (collection.CashRegister != null)
        {
            sb.AppendLine($"Caja: {collection.CashRegister.Name}");
        }
        var cajeroName = collection.Employee?.Name ?? collection.UserId;
        sb.AppendLine($"Cajero: {cajeroName}");
        sb.AppendLine($"Concepto: {cleanReason}");
        sb.AppendLine(DrawSeparator('-', width));

        // 4. Desglose de Denominaciones
        sb.AppendLine(Center("DESGLOSE DE EFECTIVO", width));
        sb.AppendLine(DrawSeparator('-', width));

        int denomW = (int)(width * 0.45);
        int qtyW = (int)(width * 0.20);
        int totalW = width - denomW - qtyW;

        sb.AppendLine(FormatTableRow(width,
            ("DENOMINACIÓN", denomW, false),
            ("CANTIDAD", qtyW, true),
            ("IMPORTE", totalW, true)
        ));
        sb.AppendLine(DrawSeparator('-', width));

        if (collection.Denominations != null && collection.Denominations.Any())
        {
            foreach (var denom in collection.Denominations.OrderByDescending(d => d.Type.GetValue()))
            {
                sb.AppendLine(FormatTableRow(width,
                    (FormatDenominationName(denom.Type), denomW, false),
                    (denom.Quantity.ToString(), qtyW, true),
                    (denom.TotalValue.ToString("C2"), totalW, true)
                ));
            }
        }
        else
        {
            sb.AppendLine(FormatTableRow(width,
                ("Efectivo General", denomW, false),
                ("1", qtyW, true),
                (collection.Amount.ToString("C2"), totalW, true)
            ));
        }

        sb.AppendLine(DrawSeparator('=', width));
        sb.AppendLine(FormatTableRow(width, ("IMPORTE TOTAL:", width - totalW, true), (collection.Amount.ToString("C2"), totalW, true)));
        sb.AppendLine();

        // 5. Bloque de firmas formal
        sb.AppendLine();
        sb.AppendLine(FormatTableRow(width,
            ("_____________________", width / 2, false),
            ("_____________________", width - (width / 2), true)
        ));
        sb.AppendLine(FormatTableRow(width,
            ("Firma de Cajero", width / 2, false),
            ("Firma de Supervisor", width - (width / 2), true)
        ));
        sb.AppendLine();
        sb.AppendLine();

        AppendFooter(sb, config, width);

        // Corte parcial
        sb.Append("\x1B\x69");

        return sb.ToString();
    }

    // ──────────────────────────────────────────────────────────────────────────
    // 5. CORTE DE CAJA (ARQUEOS Y CIERRES)
    // ──────────────────────────────────────────────────────────────────────────
    public async Task<string> GenerateCashCutTicketAsync(Guid cutId, CancellationToken cancellationToken = default)
    {
        var cut = await _context.CashCuts
            .Include(c => c.CashRegister)
                .ThenInclude(r => r!.Branch)
                    .ThenInclude(b => b!.Address)
            .Include(c => c.Employee)
            .Include(c => c.Shift)
            .FirstOrDefaultAsync(c => c.Id == cutId, cancellationToken)
            ?? throw new KeyNotFoundException($"Corte de caja {cutId} no encontrado");

        var config = await _context.SystemConfigurations.FirstOrDefaultAsync(cancellationToken);
        var width = config?.TicketWidth ?? 48;

        var sb = new StringBuilder();

        // 1. Encabezado
        AppendHeader(sb, config, cut.CashRegister?.Branch, width);

        // 2. Título de comprobante
        sb.AppendLine(Center("CORTE DE CAJA (ARQUEO FISICO)", width));
        sb.AppendLine(DrawSeparator('=', width));
        sb.AppendLine();

        // 3. Detalles de Corte y Turno
        sb.AppendLine($"Folio Corte: {cut.Id.ToString().Substring(0, 8).ToUpperInvariant()}");
        sb.AppendLine($"Fecha Corte: {cut.CutDate.ToLocalTime():dd/MM/yyyy HH:mm}");
        if (cut.CashRegister != null)
        {
            sb.AppendLine($"Caja: {cut.CashRegister.Name}");
        }
        var cajeroName = cut.Employee?.Name ?? cut.UserId;
        sb.AppendLine($"Cajero: {cajeroName}");

        if (cut.Shift != null)
        {
            sb.AppendLine($"Turno ID: {cut.Shift.Id.ToString().Substring(0, 8).ToUpperInvariant()}");
            sb.AppendLine($"Apertura: {cut.Shift.StartTime.ToLocalTime():dd/MM/yyyy HH:mm}");
            if (cut.Shift.EndTime.HasValue)
            {
                sb.AppendLine($"Cierre: {cut.Shift.EndTime.Value.ToLocalTime():dd/MM/yyyy HH:mm}");
            }
        }
        sb.AppendLine(DrawSeparator('-', width));

        // 4. Estado de Efectivo en Gaveta (Balance de Caja)
        sb.AppendLine(Center("BALANCE GENERAL DE EFECTIVO", width));
        sb.AppendLine(DrawSeparator('-', width));

        int labelW = width - 14;
        int valueW = 14;

        var initialCash = cut.Shift?.InitialCash ?? 0m;
        
        // Cargar entradas y salidas de este turno
        var cashCollections = await _context.CashCollections
            .Where(c => c.ShiftId == cut.ShiftId)
            .ToListAsync(cancellationToken);
        var totalInflows = cashCollections.Where(c => c.Reason.StartsWith("[INFLOW]", StringComparison.OrdinalIgnoreCase)).Sum(c => c.Amount);
        var totalOutflows = cashCollections.Where(c => c.Reason.StartsWith("[OUTFLOW]", StringComparison.OrdinalIgnoreCase)).Sum(c => c.Amount);

        // Obtener ventas en efectivo
        var shiftCashSales = cut.Shift?.PaymentMethodTotals?
            .FirstOrDefault(p => p.PaymentMethod == PaymentMethodType.Cash)?.Amount ?? 0m;

        var cashReturns = cut.Shift?.TotalCashReturns ?? 0m;

        sb.AppendLine(FormatTableRow(width, ("Fondo Inicial Caja:", labelW, false), (initialCash.ToString("C2"), valueW, true)));
        sb.AppendLine(FormatTableRow(width, ("(+) Ventas Efectivo:", labelW, false), (shiftCashSales.ToString("C2"), valueW, true)));
        sb.AppendLine(FormatTableRow(width, ("(+) Dotación Morralla:", labelW, false), (totalInflows.ToString("C2"), valueW, true)));
        sb.AppendLine(FormatTableRow(width, ("(-) Recolección Efect.:", labelW, false), (totalOutflows.ToString("C2"), valueW, true)));
        sb.AppendLine(FormatTableRow(width, ("(-) Devolución Efect.:", labelW, false), (cashReturns.ToString("C2"), valueW, true)));
        sb.AppendLine(DrawSeparator('-', width));
        sb.AppendLine(FormatTableRow(width, ("(=) Efectivo Esperado:", labelW, false), (cut.SystemExpectedCash.ToString("C2"), valueW, true)));
        sb.AppendLine(FormatTableRow(width, ("(=) Efectivo Físico:", labelW, false), (cut.DeclaredPhysicalCash.ToString("C2"), valueW, true)));
        sb.AppendLine(DrawSeparator('-', width));

        // Calcular Diferencia
        string diffStatus = "CUADRADO";
        if (cut.Difference < 0)
        {
            diffStatus = "FALTANTE";
        }
        else if (cut.Difference > 0)
        {
            diffStatus = "SOBRANTE";
        }

        sb.AppendLine(FormatTableRow(width, ($"DIFERENCIA ({diffStatus}):", labelW, false), (cut.Difference.ToString("C2"), valueW, true)));
        sb.AppendLine(DrawSeparator('-', width));
        sb.AppendLine();

        // 5. Desglose de Ventas por Métodos de Pago
        if (cut.Shift?.PaymentMethodTotals != null && cut.Shift.PaymentMethodTotals.Any())
        {
            sb.AppendLine(Center("VENTAS TOTALES POR MÉTODO", width));
            sb.AppendLine(DrawSeparator('-', width));
            foreach (var breakdown in cut.Shift.PaymentMethodTotals)
            {
                sb.AppendLine(FormatTableRow(width,
                    (GetPaymentMethodTranslation(breakdown.PaymentMethod), labelW, false),
                    (breakdown.Amount.ToString("C2"), valueW, true)
                ));
            }
            sb.AppendLine(DrawSeparator('-', width));
            sb.AppendLine();
        }

        // 6. Desglose de Efectivo Físico Declarado (Denominaciones)
        if (cut.CashDenominations != null && cut.CashDenominations.Any())
        {
            sb.AppendLine(Center("DESGLOSE DE EFECTIVO FISICO", width));
            sb.AppendLine(DrawSeparator('-', width));
            int denomW = (int)(width * 0.45);
            int qtyColW = (int)(width * 0.20);
            int impColW = width - denomW - qtyColW;

            sb.AppendLine(FormatTableRow(width,
                ("DENOMINACIÓN", denomW, false),
                ("CANTIDAD", qtyColW, true),
                ("IMPORTE", impColW, true)
            ));
            sb.AppendLine(DrawSeparator('-', width));

            foreach (var denom in cut.CashDenominations.OrderByDescending(d => d.Type.GetValue()))
            {
                sb.AppendLine(FormatTableRow(width,
                    (FormatDenominationName(denom.Type), denomW, false),
                    (denom.Quantity.ToString(), qtyColW, true),
                    (denom.TotalValue.ToString("C2"), impColW, true)
                ));
            }
            sb.AppendLine(DrawSeparator('-', width));
            sb.AppendLine();
        }

        // 7. Bloque de firmas formal
        sb.AppendLine();
        sb.AppendLine(FormatTableRow(width,
            ("_____________________", width / 2, false),
            ("_____________________", width - (width / 2), true)
        ));
        sb.AppendLine(FormatTableRow(width,
            ("Firma de Cajero", width / 2, false),
            ("Firma de Auditor", width - (width / 2), true)
        ));
        sb.AppendLine();
        sb.AppendLine();

        AppendFooter(sb, config, width);

        // Corte parcial
        sb.Append("\x1B\x69");

        return sb.ToString();
    }

    // ──────────────────────────────────────────────────────────────────────────
    // HELPERS DE ESTRUCTURA Y FORMATEO DE TICKETS
    // ──────────────────────────────────────────────────────────────────────────
    private static void AppendHeader(StringBuilder sb, SystemConfiguration? config, Branch? branch, int width)
    {
        // Si existe un encabezado personalizado configurado, lo imprimimos de forma primordial
        if (!string.IsNullOrEmpty(config?.TicketHeader))
        {
            var headerLines = config.TicketHeader.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
            foreach (var line in headerLines)
            {
                sb.AppendLine(Center(line.Trim(), width));
            }
            sb.AppendLine(DrawSeparator('=', width));
            sb.AppendLine();
            return;
        }

        // De lo contrario, usamos un formato fiscal / corporativo profesional por default
        if (config != null)
        {
            sb.AppendLine(Center(config.CompanyName.ToUpperInvariant(), width));
            if (!string.IsNullOrEmpty(config.TaxId))
            {
                sb.AppendLine(Center($"RFC EMISOR: {config.TaxId.ToUpperInvariant()}", width));
            }
        }

        if (branch != null)
        {
            sb.AppendLine(Center($"SUCURSAL: {branch.Name.ToUpperInvariant()}", width));
            if (branch.Address != null && !string.IsNullOrEmpty(branch.Address.Street))
            {
                sb.AppendLine(Center(branch.Address.Street, width));
                sb.AppendLine(Center($"CP: {branch.Address.ZipCode}, {branch.Address.City}, {branch.Address.State}", width));
            }
            if (!string.IsNullOrEmpty(branch.Phone))
            {
                sb.AppendLine(Center($"TEL: {branch.Phone}", width));
            }
        }
        else if (config?.FiscalAddress != null)
        {
            sb.AppendLine(Center(config.FiscalAddress.Street, width));
            sb.AppendLine(Center($"CP: {config.FiscalAddress.ZipCode}, {config.FiscalAddress.City}, {config.FiscalAddress.State}", width));
        }

        sb.AppendLine(DrawSeparator('=', width));
        sb.AppendLine();
    }

    private static void AppendFooter(StringBuilder sb, SystemConfiguration? config, int width)
    {
        sb.AppendLine(DrawSeparator('=', width));
        sb.AppendLine();

        if (!string.IsNullOrEmpty(config?.TicketFooter))
        {
            var footerLines = config.TicketFooter.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
            foreach (var line in footerLines)
            {
                sb.AppendLine(Center(line.Trim(), width));
            }
        }
        else
        {
            sb.AppendLine(Center("¡GRACIAS POR SU COMPRA!", width));
            sb.AppendLine(Center("Vuelva Pronto", width));
        }

        sb.AppendLine();
        sb.AppendLine();
        sb.AppendLine();
    }

    private static string Center(string text, int width)
    {
        if (text.Length >= width) return text;
        int padding = (width - text.Length) / 2;
        return text.PadLeft(text.Length + padding).PadRight(width);
    }

    private static string WrapText(string text, int width)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;
        var sb = new StringBuilder();
        for (int i = 0; i < text.Length; i += width)
        {
            if (i + width < text.Length)
                sb.AppendLine(text.Substring(i, width));
            else
                sb.AppendLine(text.Substring(i));
        }
        return sb.ToString().TrimEnd();
    }

    private static string DrawSeparator(char symbol, int width)
    {
        return new string(symbol, width);
    }

    private static string FormatTableRow(int width, params (string text, int colWidth, bool alignRight)[] columns)
    {
        var sb = new StringBuilder();
        int currentPos = 0;

        for (int i = 0; i < columns.Length; i++)
        {
            var col = columns[i];
            string text = col.text ?? "";
            int colWidth = col.colWidth;

            if (text.Length > colWidth)
            {
                // Para la columna del producto o la primera columna, si excede, agregamos puntos suspensivos
                if (i == 0 && colWidth > 3)
                {
                    text = text.Substring(0, colWidth - 3) + "...";
                }
                else
                {
                    text = text.Substring(0, colWidth);
                }
            }

            string padded = col.alignRight ? text.PadLeft(colWidth) : text.PadRight(colWidth);
            sb.Append(padded);
            currentPos += colWidth;
        }

        if (currentPos < width)
        {
            sb.Append(new string(' ', width - currentPos));
        }

        return sb.ToString();
    }

    private static string GetPaymentMethodTranslation(PaymentMethodType method)
    {
        return method switch
        {
            PaymentMethodType.Cash => "Efectivo",
            PaymentMethodType.CreditCard => "Tarj. Crédito",
            PaymentMethodType.DebitCard => "Tarj. Débito",
            PaymentMethodType.Transfer => "Transferencia",
            PaymentMethodType.Check => "Cheque",
            _ => method.ToString()
        };
    }

    private static string GetRefundMethodTranslation(RefundMethod method)
    {
        return method switch
        {
            RefundMethod.Cash => "Efectivo",
            RefundMethod.Card => "Tarjeta",
            RefundMethod.StoreCredit => "Crédito Tienda",
            _ => method.ToString()
        };
    }

    private static string FormatDenominationName(DenominationType denomination)
    {
        var val = denomination.GetValue();
        bool isBill = denomination.ToString().StartsWith("Bill", StringComparison.OrdinalIgnoreCase);
        return $"{(isBill ? "Billete" : "Moneda")} {val:C2}";
    }

    private static string GetCfdiUsageDescription(CfdiUsage usage)
    {
        return usage switch
        {
            CfdiUsage.GeneralExpense => "G03 - Gastos en general",
            CfdiUsage.Acquisition => "G01 - Adquisición de mercancías",
            CfdiUsage.ToDefine => "S01 - Sin efectos fiscales",
            _ => usage.ToString()
        };
    }
}
