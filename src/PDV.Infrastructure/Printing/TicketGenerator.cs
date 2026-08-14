using Microsoft.EntityFrameworkCore;
using PDV.Application.Common.Interfaces;
using PDV.Infrastructure.Identity;
using PDV.Domain.Entities;
using PDV.Domain.Enums;
using PDV.Domain.ValueObjects;
using PDV.Infrastructure.Persistence;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace PDV.Infrastructure.Printing;

public class TicketGenerator : ITicketGenerator
{
    private readonly AppDbContext _context;
    private readonly IIdentityService _identityService;

    public TicketGenerator(AppDbContext context, IIdentityService identityService)
    {
        _context = context;
        _identityService = identityService;
    }

    public async Task<string> GenerateSaleTicketAsync(Guid saleId, CancellationToken cancellationToken = default, int? widthCharacters = null)
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
        var width = widthCharacters ?? 42;

        var user = !string.IsNullOrEmpty(sale.UserId)
            ? await _context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == sale.UserId, cancellationToken)
            : null;

        var variables = new Dictionary<string, string>
        {
            { "{CompanyName}", config?.CompanyName ?? string.Empty },
            { "{TaxId}", config?.TaxId ?? string.Empty },
            { "{BranchName}", sale.Branch?.Name ?? string.Empty },
            { "{BranchAddress}", sale.Branch?.Address != null ? $"{sale.Branch.Address.Street}, CP {sale.Branch.Address.ZipCode}" : string.Empty },
            { "{BranchPhone}", sale.Branch?.Phone ?? string.Empty },
            { "{Folio}", sale.SaleNumber.ToString() },
            { "{Date}", sale.Date.ToLocalTime().ToString("dd/MM/yyyy HH:mm") },
            { "{CashRegisterName}", sale.CashRegister?.Name ?? string.Empty },
            { "{UserFullName}", user?.FullName ?? sale.UserId ?? string.Empty },
            { "{ClientName}", sale.Client?.Name ?? "Público General" },
            { "{Subtotal}", sale.Subtotal.ToString("C2") },
            { "{Tax}", sale.Taxes.Sum(t => t.TaxAmount).ToString("C2") },
            { "{Total}", sale.TotalAmount.ToString("C2") },
            { "{PaymentMethod}", GetPaymentMethodTranslation(sale.PaymentMethod) }
        };

        var tableItems = sale.Items.Select(item => new TicketTableItem
        {
            Name = item.ProductName,
            Quantity = item.Quantity.ToString("0.##"),
            Price = item.UnitPrice.ToString("C2"),
            Total = item.TotalAmount.ToString("C2")
        }).ToList();

        var templateJson = await _context.TicketTemplates
            .FirstOrDefaultAsync(t => t.TemplateType == TicketTemplateType.Sale && t.IsDefault, cancellationToken);
        string jsonStr = templateJson?.ContentJson ?? GetDefaultTemplateJson(TicketTemplateType.Sale);
        var template = JsonSerializer.Deserialize<TicketTemplateJson>(jsonStr, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new TicketTemplateJson();

        var ticketText = DynamicTicketRenderer.Render(template, variables, tableItems, width);
        ticketText = await ProcessLogoPlaceholderAsync(ticketText, cancellationToken);
        var sb = new StringBuilder(ticketText);

        sb.Append("\x1B\x69");
        return sb.ToString();
    }

    public async Task<string> GenerateInvoiceTicketAsync(Guid invoiceId, CancellationToken cancellationToken = default, int? widthCharacters = null)
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
        var width = widthCharacters ?? 42;

        var variables = new Dictionary<string, string>
        {
            { "{CompanyName}", config?.CompanyName ?? string.Empty },
            { "{TaxId}", config?.TaxId ?? string.Empty },
            { "{FiscalRegime}", config?.FiscalRegime ?? string.Empty },
            { "{CsdSerialNumber}", config?.CsdSerialNumber ?? string.Empty },
            { "{ReceiverName}", invoice.ReceiverName.ToUpperInvariant() },
            { "{ReceiverTaxId}", invoice.ReceiverTaxId.ToUpperInvariant() },
            { "{CfdiUsage}", GetCfdiUsageDescription(invoice.CfdiUsage) },
            { "{InvoiceNumber}", invoice.InvoiceNumber },
            { "{InvoiceDate}", invoice.InvoiceDate.ToLocalTime().ToString("dd/MM/yyyy HH:mm") },
            { "{SaleNumber}", invoice.Sale?.SaleNumber.ToString() ?? string.Empty },
            { "{Subtotal}", invoice.Subtotal.ToString("C2") },
            { "{Tax}", invoice.Tax.ToString("C2") },
            { "{Total}", invoice.Total.ToString("C2") },
            { "{Uuid}", invoice.Uuid ?? string.Empty },
            { "{NoCertificadoSAT}", invoice.NoCertificadoSAT ?? string.Empty },
            { "{StampedAt}", invoice.StampedAt.HasValue ? invoice.StampedAt.Value.ToString("dd/MM/yyyy HH:mm:ss") : string.Empty },
            { "{InvoiceType}", invoice.Type switch {
                InvoiceType.Customer => "CFDI DE INGRESO (CLIENTE)",
                InvoiceType.Global => "CFDI DE INGRESO (GLOBAL)",
                InvoiceType.CreditNote => "CFDI DE EGRESO (NOTA DE CRÉDITO)",
                _ => "CFDI"
            }}
        };

        var tableItems = new List<TicketTableItem>();
        if (invoice.Sale != null && invoice.Sale.Items.Any())
        {
            tableItems = invoice.Sale.Items.Select(item => new TicketTableItem
            {
                Name = item.ProductName,
                Quantity = item.Quantity.ToString("0.##"),
                Price = item.UnitPrice.ToString("C2"),
                Total = item.TotalAmount.ToString("C2")
            }).ToList();
        }
        else
        {
            tableItems.Add(new TicketTableItem
            {
                Name = "CONSOLIDADO GLOBAL DE VENTAS",
                Quantity = "1",
                Price = invoice.Subtotal.ToString("C2"),
                Total = invoice.Subtotal.ToString("C2")
            });
        }

        var templateJson = await _context.TicketTemplates
            .FirstOrDefaultAsync(t => t.TemplateType == TicketTemplateType.Invoice && t.IsDefault, cancellationToken);
        string jsonStr = templateJson?.ContentJson ?? GetDefaultTemplateJson(TicketTemplateType.Invoice);
        var template = JsonSerializer.Deserialize<TicketTemplateJson>(jsonStr, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new TicketTemplateJson();

        var ticketText = DynamicTicketRenderer.Render(template, variables, tableItems, width);
        ticketText = await ProcessLogoPlaceholderAsync(ticketText, cancellationToken);
        var sb = new StringBuilder(ticketText);

        sb.Append("\x1B\x69");
        return sb.ToString();
    }

    public async Task<string> GenerateReturnTicketAsync(Guid returnId, CancellationToken cancellationToken = default, int? widthCharacters = null)
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
        var width = widthCharacters ?? 42;

        var user = !string.IsNullOrEmpty(returnSale.UserId)
            ? await _context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == returnSale.UserId, cancellationToken)
            : null;

        var folioText = string.IsNullOrEmpty(returnSale.Series) ? returnSale.Folio.ToString() : $"{returnSale.Series}{returnSale.Folio}";

        var variables = new Dictionary<string, string>
        {
            { "{CompanyName}", config?.CompanyName ?? string.Empty },
            { "{TaxId}", config?.TaxId ?? string.Empty },
            { "{BranchName}", returnSale.Branch?.Name ?? string.Empty },
            { "{BranchAddress}", returnSale.Branch?.Address != null ? $"{returnSale.Branch.Address.Street}, CP {returnSale.Branch.Address.ZipCode}" : string.Empty },
            { "{BranchPhone}", returnSale.Branch?.Phone ?? string.Empty },
            { "{Folio}", folioText },
            { "{Date}", returnSale.ReturnDate.ToLocalTime().ToString("dd/MM/yyyy HH:mm") },
            { "{SaleNumber}", returnSale.Sale?.SaleNumber.ToString() ?? string.Empty },
            { "{CashRegisterName}", returnSale.CashRegister?.Name ?? string.Empty },
            { "{UserFullName}", user?.FullName ?? returnSale.UserId },
            { "{ClientName}", returnSale.Client?.Name ?? "Público General" },
            { "{Reason}", returnSale.Reason },
            { "{Subtotal}", returnSale.Subtotal.ToString("C2") },
            { "{Tax}", returnSale.Taxes.Sum(t => t.TaxAmount).ToString("C2") },
            { "{Total}", returnSale.TotalRefund.ToString("C2") },
            { "{RefundMethod}", GetRefundMethodTranslation(returnSale.RefundMethod) }
        };

        var tableItems = returnSale.Items.Select(item => new TicketTableItem
        {
            Name = item.ProductName,
            Quantity = item.Quantity.ToString("0.##"),
            Price = item.UnitPrice.ToString("C2"),
            Total = item.TotalAmount.ToString("C2")
        }).ToList();

        var templateJson = await _context.TicketTemplates
            .FirstOrDefaultAsync(t => t.TemplateType == TicketTemplateType.Return && t.IsDefault, cancellationToken);
        string jsonStr = templateJson?.ContentJson ?? GetDefaultTemplateJson(TicketTemplateType.Return);
        var template = JsonSerializer.Deserialize<TicketTemplateJson>(jsonStr, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new TicketTemplateJson();

        var ticketText = DynamicTicketRenderer.Render(template, variables, tableItems, width);
        ticketText = await ProcessLogoPlaceholderAsync(ticketText, cancellationToken);
        var sb = new StringBuilder(ticketText);

        sb.Append("\x1B\x69");
        return sb.ToString();
    }

    public async Task<string> GenerateCashCollectionTicketAsync(Guid collectionId, CancellationToken cancellationToken = default, int? widthCharacters = null)
    {
        var collection = await _context.CashCollections
            .Include(c => c.CashRegister)
                .ThenInclude(r => r!.Branch)
                    .ThenInclude(b => b!.Address)
            .FirstOrDefaultAsync(c => c.Id == collectionId, cancellationToken)
            ?? throw new KeyNotFoundException($"Movimiento de caja {collectionId} no encontrado");

        var config = await _context.SystemConfigurations.FirstOrDefaultAsync(cancellationToken);
        var width = widthCharacters ?? 42;

        var user = !string.IsNullOrEmpty(collection.UserId)
            ? await _context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == collection.UserId, cancellationToken)
            : null;

        bool isInflow = collection.Reason.StartsWith("[INFLOW]", StringComparison.OrdinalIgnoreCase);
        var ticketTitle = isInflow ? "DOTACIÓN DE MORRALLA" : "RECOLECCIÓN DE EFECTIVO";
        var cleanReason = collection.Reason
            .Replace("[INFLOW]", "", StringComparison.OrdinalIgnoreCase)
            .Replace("[OUTFLOW]", "", StringComparison.OrdinalIgnoreCase)
            .Trim();

        var variables = new Dictionary<string, string>
        {
            { "{CompanyName}", config?.CompanyName ?? string.Empty },
            { "{TaxId}", config?.TaxId ?? string.Empty },
            { "{BranchName}", collection.CashRegister?.Branch?.Name ?? string.Empty },
            { "{BranchAddress}", collection.CashRegister?.Branch?.Address != null ? $"{collection.CashRegister.Branch.Address.Street}, CP {collection.CashRegister.Branch.Address.ZipCode}" : string.Empty },
            { "{TicketTitle}", ticketTitle },
            { "{Folio}", collection.Id.ToString().Substring(0, 8).ToUpperInvariant() },
            { "{Date}", collection.CollectionDate.ToLocalTime().ToString("dd/MM/yyyy HH:mm") },
            { "{CashRegisterName}", collection.CashRegister?.Name ?? string.Empty },
            { "{UserFullName}", user?.FullName ?? collection.UserId },
            { "{Reason}", cleanReason },
            { "{Total}", collection.Amount.ToString("C2") }
        };

        var templateJson = await _context.TicketTemplates
            .FirstOrDefaultAsync(t => t.TemplateType == TicketTemplateType.CashCollection && t.IsDefault, cancellationToken);
        string jsonStr = templateJson?.ContentJson ?? GetDefaultTemplateJson(TicketTemplateType.CashCollection);
        var template = JsonSerializer.Deserialize<TicketTemplateJson>(jsonStr, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new TicketTemplateJson();

        var ticketText = DynamicTicketRenderer.Render(template, variables, new List<TicketTableItem>(), width);
        ticketText = await ProcessLogoPlaceholderAsync(ticketText, cancellationToken);
        var sb = new StringBuilder(ticketText);

        sb.Append("\x1B\x69");
        return sb.ToString();
    }

    public async Task<string> GenerateCashCutTicketAsync(Guid cutId, CancellationToken cancellationToken = default, int? widthCharacters = null)
    {
        var cut = await _context.CashCuts
            .Include(c => c.CashRegister)
                .ThenInclude(r => r!.Branch)
                    .ThenInclude(b => b!.Address)
            .Include(c => c.Shift)
            .FirstOrDefaultAsync(c => c.Id == cutId, cancellationToken)
            ?? throw new KeyNotFoundException($"Corte de caja {cutId} no encontrado");

        var config = await _context.SystemConfigurations.FirstOrDefaultAsync(cancellationToken);
        var width = widthCharacters ?? 42;

        var user = !string.IsNullOrEmpty(cut.UserId)
            ? await _context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == cut.UserId, cancellationToken)
            : null;

        var initialCash = cut.Shift?.InitialCash ?? 0m;
        var cashCollections = await _context.CashCollections
            .Where(c => c.ShiftId == cut.ShiftId)
            .ToListAsync(cancellationToken);
        var totalInflows = cashCollections.Where(c => c.Reason.StartsWith("[INFLOW]", StringComparison.OrdinalIgnoreCase)).Sum(c => c.Amount);
        var totalOutflows = cashCollections.Where(c => c.Reason.StartsWith("[OUTFLOW]", StringComparison.OrdinalIgnoreCase)).Sum(c => c.Amount);

        var shiftCashSales = cut.Shift?.PaymentMethodTotals?
            .FirstOrDefault(p => p.PaymentMethod == PaymentMethodType.Cash)?.Amount ?? 0m;

        var cashReturns = cut.Shift?.TotalCashReturns ?? 0m;

        string diffStatus = "CUADRADO";
        if (cut.Difference < 0) diffStatus = "FALTANTE";
        else if (cut.Difference > 0) diffStatus = "SOBRANTE";

        var variables = new Dictionary<string, string>
        {
            { "{CompanyName}", config?.CompanyName ?? string.Empty },
            { "{TaxId}", config?.TaxId ?? string.Empty },
            { "{BranchName}", cut.CashRegister?.Branch?.Name ?? string.Empty },
            { "{BranchAddress}", cut.CashRegister?.Branch?.Address != null ? $"{cut.CashRegister.Branch.Address.Street}, CP {cut.CashRegister.Branch.Address.ZipCode}" : string.Empty },
            { "{Folio}", cut.Id.ToString().Substring(0, 8).ToUpperInvariant() },
            { "{Date}", cut.CutDate.ToLocalTime().ToString("dd/MM/yyyy HH:mm") },
            { "{CashRegisterName}", cut.CashRegister?.Name ?? string.Empty },
            { "{UserFullName}", user?.FullName ?? cut.UserId },
            { "{InitialCash}", initialCash.ToString("C2") },
            { "{CashSales}", shiftCashSales.ToString("C2") },
            { "{Inflows}", totalInflows.ToString("C2") },
            { "{Outflows}", totalOutflows.ToString("C2") },
            { "{Returns}", cashReturns.ToString("C2") },
            { "{ExpectedCash}", cut.SystemExpectedCash.ToString("C2") },
            { "{PhysicalCash}", cut.DeclaredPhysicalCash.ToString("C2") },
            { "{DiffStatus}", diffStatus },
            { "{Difference}", cut.Difference.ToString("C2") }
        };

        var templateJson = await _context.TicketTemplates
            .FirstOrDefaultAsync(t => t.TemplateType == TicketTemplateType.CashCut && t.IsDefault, cancellationToken);
        string jsonStr = templateJson?.ContentJson ?? GetDefaultTemplateJson(TicketTemplateType.CashCut);
        var template = JsonSerializer.Deserialize<TicketTemplateJson>(jsonStr, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new TicketTemplateJson();

        var ticketText = DynamicTicketRenderer.Render(template, variables, new List<TicketTableItem>(), width);
        ticketText = await ProcessLogoPlaceholderAsync(ticketText, cancellationToken);
        var sb = new StringBuilder(ticketText);

        sb.Append("\x1B\x69");
        return sb.ToString();
    }

    public async Task<string> GenerateOrderTicketAsync(Guid orderId, CancellationToken cancellationToken = default, int? widthCharacters = null)
    {
        var order = await _context.Orders
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == orderId, cancellationToken)
            ?? throw new KeyNotFoundException($"Pedido {orderId} no encontrado");

        var client = order.ClientId.HasValue
            ? await _context.Clients.Include(c => c.DeliveryZone).FirstOrDefaultAsync(c => c.Id == order.ClientId.Value, cancellationToken)
            : null;

        var config = await _context.SystemConfigurations.FirstOrDefaultAsync(cancellationToken);
        var width = widthCharacters ?? 42;

        decimal deliveryCost = client?.DeliveryZone?.DeliveryCost ?? 0m;

        var variables = new Dictionary<string, string>
        {
            { "{CompanyName}", config?.CompanyName ?? string.Empty },
            { "{TaxId}", config?.TaxId ?? string.Empty },
            { "{Folio}", $"{order.Series}-{order.Folio}" },
            { "{Date}", order.OrderDate.ToLocalTime().ToString("dd/MM/yyyy HH:mm") },
            { "{ClientName}", client?.Name ?? "Público General" },
            { "{ClientPhone}", client?.Phone ?? string.Empty },
            { "{ClientAddress}", client?.Address?.Street ?? "Sin dirección" },
            { "{DeliveryZoneName}", client?.DeliveryZone?.Name ?? "N/A" },
            { "{DeliveryCost}", deliveryCost.ToString("C2") },
            { "{PaymentMethod}", order.PaymentMethod == PaymentMethodType.Cash ? "Efectivo" : "Tarjeta" },
            { "{Subtotal}", order.Subtotal.ToString("C2") },
            { "{Tax}", order.TotalTax.ToString("C2") },
            { "{Total}", (order.TotalAmount + deliveryCost).ToString("C2") }
        };

        var tableItems = order.Items.Select(item => new TicketTableItem
        {
            Name = item.ProductName,
            Quantity = item.Quantity.ToString("0.##"),
            Price = item.UnitPrice.ToString("C2"),
            Total = item.TotalAmount.ToString("C2")
        }).ToList();

        var templateJson = await _context.TicketTemplates
            .FirstOrDefaultAsync(t => t.TemplateType == TicketTemplateType.Order && t.IsDefault, cancellationToken);
        string jsonStr = templateJson?.ContentJson ?? GetDefaultTemplateJson(TicketTemplateType.Order);
        var template = JsonSerializer.Deserialize<TicketTemplateJson>(jsonStr, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new TicketTemplateJson();

        var ticketText = DynamicTicketRenderer.Render(template, variables, tableItems, width);
        ticketText = await ProcessLogoPlaceholderAsync(ticketText, cancellationToken);
        var sb = new StringBuilder(ticketText);

        sb.Append("\x1B\x69");
        return sb.ToString();
    }

    public async Task<string> GenerateRouteManifestTicketAsync(Guid routeId, CancellationToken cancellationToken = default, int? widthCharacters = null)
    {
        var route = await _context.DeliveryRoutes
            .Include(r => r.Orders)
            .FirstOrDefaultAsync(r => r.Id == routeId, cancellationToken)
            ?? throw new KeyNotFoundException($"Ruta {routeId} no encontrada");

        var deliveryMan = await _identityService.GetUserByIdAsync(route.DeliveryManId, cancellationToken);

        var config = await _context.SystemConfigurations.FirstOrDefaultAsync(cancellationToken);
        var width = widthCharacters ?? 42;

        decimal totalCash = 0;
        decimal totalCard = 0;
        var ordersSb = new StringBuilder();
        int orderCount = 0;

        foreach (var order in route.Orders)
        {
            orderCount++;
            var client = order.ClientId.HasValue
                ? await _context.Clients.FindAsync(new object[] { order.ClientId.Value }, cancellationToken)
                : null;

            ordersSb.AppendLine($"#{orderCount} Pedido: {order.Series}-{order.Folio}");
            ordersSb.AppendLine($"Cliente: {client?.Name ?? "Público General"}");
            ordersSb.AppendLine($"Direcc:  {client?.Address?.Street ?? "Sin dirección"}");
            
            string payMethodStr = order.PaymentMethod == PaymentMethodType.Cash ? "Efectivo" : "Tarjeta";
            ordersSb.AppendLine($"Cobro:   {order.TotalAmount:C2} ({payMethodStr})");
            ordersSb.AppendLine(new string('-', width));

            if (order.PaymentMethod == PaymentMethodType.Cash)
                totalCash += order.TotalAmount;
            else
                totalCard += order.TotalAmount;
        }

        var variables = new Dictionary<string, string>
        {
            { "{CompanyName}", config?.CompanyName ?? string.Empty },
            { "{TaxId}", config?.TaxId ?? string.Empty },
            { "{Folio}", route.Folio.ToString() },
            { "{Date}", route.CreatedDate.ToLocalTime().ToString("dd/MM/yyyy HH:mm") },
            { "{DeliveryManName}", deliveryMan?.FullName ?? route.DeliveryManId },
            { "{OrdersList}", ordersSb.ToString() },
            { "{ExpectedCash}", totalCash.ToString("C2") },
            { "{ExpectedCard}", totalCard.ToString("C2") },
            { "{Total}", (totalCash + totalCard).ToString("C2") }
        };

        var templateJson = await _context.TicketTemplates
            .FirstOrDefaultAsync(t => t.TemplateType == TicketTemplateType.RouteManifest && t.IsDefault, cancellationToken);
        string jsonStr = templateJson?.ContentJson ?? GetDefaultTemplateJson(TicketTemplateType.RouteManifest);
        var template = JsonSerializer.Deserialize<TicketTemplateJson>(jsonStr, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new TicketTemplateJson();

        var ticketText = DynamicTicketRenderer.Render(template, variables, new List<TicketTableItem>(), width);
        ticketText = await ProcessLogoPlaceholderAsync(ticketText, cancellationToken);
        var sb = new StringBuilder(ticketText);

        sb.Append("\x1B\x69");
        return sb.ToString();
    }

    private string GetDefaultTemplateJson(TicketTemplateType type)
    {
        switch (type)
        {
            case TicketTemplateType.Sale:
                return @"{
                    ""Blocks"": [
                        { ""Type"": ""Logo"" },
                        { ""Type"": ""Text"", ""Content"": ""{CompanyName}"", ""Align"": ""Center"", ""Bold"": true, ""FontSize"": ""DoubleHeight"" },
                        { ""Type"": ""Text"", ""Content"": ""RFC: {TaxId}"", ""Align"": ""Center"" },
                        { ""Type"": ""Text"", ""Content"": ""SUCURSAL: {BranchName}"", ""Align"": ""Center"" },
                        { ""Type"": ""Text"", ""Content"": ""{BranchAddress}"", ""Align"": ""Center"" },
                        { ""Type"": ""Text"", ""Content"": ""TEL: {BranchPhone}"", ""Align"": ""Center"" },
                        { ""Type"": ""Separator"", ""SeparatorChar"": ""="" },
                        { ""Type"": ""Text"", ""Content"": ""TICKET DE VENTA"", ""Align"": ""Center"", ""Bold"": true },
                        { ""Type"": ""Separator"", ""SeparatorChar"": ""="" },
                        { ""Type"": ""KeyValue"", ""Key"": ""Folio:"", ""ValuePlaceholder"": ""{Folio}"" },
                        { ""Type"": ""KeyValue"", ""Key"": ""Fecha:"", ""ValuePlaceholder"": ""{Date}"" },
                        { ""Type"": ""KeyValue"", ""Key"": ""Caja:"", ""ValuePlaceholder"": ""{CashRegisterName}"" },
                        { ""Type"": ""KeyValue"", ""Key"": ""Cajero:"", ""ValuePlaceholder"": ""{UserFullName}"" },
                        { ""Type"": ""KeyValue"", ""Key"": ""Cliente:"", ""ValuePlaceholder"": ""{ClientName}"" },
                        { ""Type"": ""Separator"", ""SeparatorChar"": ""-"" },
                        { ""Type"": ""ItemsTable"", ""Columns"": [
                            { ""Field"": ""Name"", ""Title"": ""Producto"", ""WidthPercentage"": 50 },
                            { ""Field"": ""Quantity"", ""Title"": ""Cant"", ""WidthPercentage"": 15 },
                            { ""Field"": ""Price"", ""Title"": ""Precio"", ""WidthPercentage"": 15 },
                            { ""Field"": ""Total"", ""Title"": ""Total"", ""WidthPercentage"": 20 }
                        ], ""WrapText"": true },
                        { ""Type"": ""Totals"" },
                        { ""Type"": ""Separator"", ""SeparatorChar"": ""="" },
                        { ""Type"": ""Footer"", ""Content"": ""¡GRACIAS POR SU COMPRA!\\nVuelva Pronto"" }
                    ]
                }";

            case TicketTemplateType.Invoice:
                return @"{
                    ""Blocks"": [
                        { ""Type"": ""Logo"" },
                        { ""Type"": ""Text"", ""Content"": ""{CompanyName}"", ""Align"": ""Center"", ""Bold"": true },
                        { ""Type"": ""Text"", ""Content"": ""RFC: {TaxId}"", ""Align"": ""Center"" },
                        { ""Type"": ""Text"", ""Content"": ""FACTURA ELECTRÓNICA"", ""Align"": ""Center"", ""Bold"": true },
                        { ""Type"": ""Text"", ""Content"": ""{InvoiceType}"", ""Align"": ""Center"" },
                        { ""Type"": ""Separator"", ""SeparatorChar"": ""="" },
                        { ""Type"": ""Text"", ""Content"": ""DATOS DEL EMISOR:"", ""Align"": ""Left"", ""Bold"": true },
                        { ""Type"": ""Text"", ""Content"": ""Régimen Fiscal: {FiscalRegime}"", ""Align"": ""Left"" },
                        { ""Type"": ""Text"", ""Content"": ""No. Certificado: {CsdSerialNumber}"", ""Align"": ""Left"" },
                        { ""Type"": ""Separator"", ""SeparatorChar"": ""-"" },
                        { ""Type"": ""Text"", ""Content"": ""DATOS DEL RECEPTOR:"", ""Align"": ""Left"", ""Bold"": true },
                        { ""Type"": ""Text"", ""Content"": ""Nombre: {ReceiverName}"", ""Align"": ""Left"" },
                        { ""Type"": ""Text"", ""Content"": ""RFC: {ReceiverTaxId}"", ""Align"": ""Left"" },
                        { ""Type"": ""Text"", ""Content"": ""Uso CFDI: {CfdiUsage}"", ""Align"": ""Left"" },
                        { ""Type"": ""Separator"", ""SeparatorChar"": ""-"" },
                        { ""Type"": ""KeyValue"", ""Key"": ""Serie/Folio:"", ""ValuePlaceholder"": ""{InvoiceNumber}"" },
                        { ""Type"": ""KeyValue"", ""Key"": ""Fecha Emisión:"", ""ValuePlaceholder"": ""{InvoiceDate}"" },
                        { ""Type"": ""KeyValue"", ""Key"": ""Nota de Origen:"", ""ValuePlaceholder"": ""{SaleNumber}"" },
                        { ""Type"": ""Separator"", ""SeparatorChar"": ""-"" },
                        { ""Type"": ""ItemsTable"", ""Columns"": [
                            { ""Field"": ""Name"", ""Title"": ""Concepto"", ""WidthPercentage"": 50 },
                            { ""Field"": ""Quantity"", ""Title"": ""Cant"", ""WidthPercentage"": 15 },
                            { ""Field"": ""Price"", ""Title"": ""Precio"", ""WidthPercentage"": 15 },
                            { ""Field"": ""Total"", ""Title"": ""Total"", ""WidthPercentage"": 20 }
                        ], ""WrapText"": true },
                        { ""Type"": ""Totals"" },
                        { ""Type"": ""Separator"", ""SeparatorChar"": ""="" },
                        { ""Type"": ""Text"", ""Content"": ""DATOS FISCALES DEL CFDI"", ""Align"": ""Center"", ""Bold"": true },
                        { ""Type"": ""Separator"", ""SeparatorChar"": ""="" },
                        { ""Type"": ""Text"", ""Content"": ""UUID: {Uuid}"", ""Align"": ""Left"" },
                        { ""Type"": ""Text"", ""Content"": ""Certificado SAT: {NoCertificadoSAT}"", ""Align"": ""Left"" },
                        { ""Type"": ""Text"", ""Content"": ""Fecha Timbrado: {StampedAt}"", ""Align"": ""Left"" },
                        { ""Type"": ""BarcodeOrQr"", ""CodeType"": ""QR"", ""CodifiedValue"": ""https://verificacfdi.facturaelectronica.sat.gob.mx/default.aspx?id={Uuid}"" },
                        { ""Type"": ""Text"", ""Content"": ""Este documento es una representación impresa de un CFDI"", ""Align"": ""Center"" },
                        { ""Type"": ""Separator"", ""SeparatorChar"": ""="" },
                        { ""Type"": ""Footer"", ""Content"": ""¡GRACIAS POR SU PREFERENCIA!"" }
                    ]
                }";

            case TicketTemplateType.Return:
                return @"{
                    ""Blocks"": [
                        { ""Type"": ""Logo"" },
                        { ""Type"": ""Text"", ""Content"": ""{CompanyName}"", ""Align"": ""Center"", ""Bold"": true },
                        { ""Type"": ""Text"", ""Content"": ""TICKET DE DEVOLUCIÓN"", ""Align"": ""Center"", ""Bold"": true },
                        { ""Type"": ""Separator"", ""SeparatorChar"": ""="" },
                        { ""Type"": ""KeyValue"", ""Key"": ""Folio Dev:"", ""ValuePlaceholder"": ""{Folio}"" },
                        { ""Type"": ""KeyValue"", ""Key"": ""Fecha:"", ""ValuePlaceholder"": ""{Date}"" },
                        { ""Type"": ""KeyValue"", ""Key"": ""Ticket Orig:"", ""ValuePlaceholder"": ""{SaleNumber}"" },
                        { ""Type"": ""KeyValue"", ""Key"": ""Cajero:"", ""ValuePlaceholder"": ""{UserFullName}"" },
                        { ""Type"": ""Text"", ""Content"": ""Motivo: {Reason}"", ""Align"": ""Left"" },
                        { ""Type"": ""Separator"", ""SeparatorChar"": ""-"" },
                        { ""Type"": ""ItemsTable"", ""Columns"": [
                            { ""Field"": ""Name"", ""Title"": ""Producto"", ""WidthPercentage"": 50 },
                            { ""Field"": ""Quantity"", ""Title"": ""Cant"", ""WidthPercentage"": 15 },
                            { ""Field"": ""Price"", ""Title"": ""Precio"", ""WidthPercentage"": 15 },
                            { ""Field"": ""Total"", ""Title"": ""Total"", ""WidthPercentage"": 20 }
                        ], ""WrapText"": true },
                        { ""Type"": ""Totals"" },
                        { ""Type"": ""Separator"", ""SeparatorChar"": ""="" },
                        { ""Type"": ""Text"", ""Content"": ""_________________________\nFirma de Conformidad Cliente"", ""Align"": ""Center"" },
                        { ""Type"": ""Footer"", ""Content"": ""Vuelva Pronto"" }
                    ]
                }";

            case TicketTemplateType.CashCollection:
                return @"{
                    ""Blocks"": [
                        { ""Type"": ""Logo"" },
                        { ""Type"": ""Text"", ""Content"": ""{CompanyName}"", ""Align"": ""Center"", ""Bold"": true },
                        { ""Type"": ""Text"", ""Content"": ""{TicketTitle}"", ""Align"": ""Center"", ""Bold"": true },
                        { ""Type"": ""Separator"", ""SeparatorChar"": ""="" },
                        { ""Type"": ""KeyValue"", ""Key"": ""Folio:"", ""ValuePlaceholder"": ""{Folio}"" },
                        { ""Type"": ""KeyValue"", ""Key"": ""Fecha:"", ""ValuePlaceholder"": ""{Date}"" },
                        { ""Type"": ""KeyValue"", ""Key"": ""Caja:"", ""ValuePlaceholder"": ""{CashRegisterName}"" },
                        { ""Type"": ""KeyValue"", ""Key"": ""Cajero:"", ""ValuePlaceholder"": ""{UserFullName}"" },
                        { ""Type"": ""Text"", ""Content"": ""Concepto: {Reason}"", ""Align"": ""Left"" },
                        { ""Type"": ""Separator"", ""SeparatorChar"": ""-"" },
                        { ""Type"": ""KeyValue"", ""Key"": ""IMPORTE TOTAL:"", ""ValuePlaceholder"": ""{Total}"", ""Bold"": true },
                        { ""Type"": ""Separator"", ""SeparatorChar"": ""="" },
                        { ""Type"": ""Text"", ""Content"": ""_____________________     _____________________\nFirma de Cajero          Firma de Supervisor"", ""Align"": ""Center"" }
                    ]
                }";

            case TicketTemplateType.CashCut:
                return @"{
                    ""Blocks"": [
                        { ""Type"": ""Logo"" },
                        { ""Type"": ""Text"", ""Content"": ""{CompanyName}"", ""Align"": ""Center"", ""Bold"": true },
                        { ""Type"": ""Text"", ""Content"": ""CORTE DE CAJA (ARQUEO FISICO)"", ""Align"": ""Center"", ""Bold"": true },
                        { ""Type"": ""Separator"", ""SeparatorChar"": ""="" },
                        { ""Type"": ""KeyValue"", ""Key"": ""Folio Corte:"", ""ValuePlaceholder"": ""{Folio}"" },
                        { ""Type"": ""KeyValue"", ""Key"": ""Fecha Corte:"", ""ValuePlaceholder"": ""{Date}"" },
                        { ""Type"": ""KeyValue"", ""Key"": ""Caja:"", ""ValuePlaceholder"": ""{CashRegisterName}"" },
                        { ""Type"": ""KeyValue"", ""Key"": ""Cajero:"", ""ValuePlaceholder"": ""{UserFullName}"" },
                        { ""Type"": ""Separator"", ""SeparatorChar"": ""-"" },
                        { ""Type"": ""Text"", ""Content"": ""BALANCE GENERAL DE EFECTIVO"", ""Align"": ""Center"", ""Bold"": true },
                        { ""Type"": ""Separator"", ""SeparatorChar"": ""-"" },
                        { ""Type"": ""KeyValue"", ""Key"": ""Fondo Inicial Caja:"", ""ValuePlaceholder"": ""{InitialCash}"" },
                        { ""Type"": ""KeyValue"", ""Key"": ""(+) Ventas Efectivo:"", ""ValuePlaceholder"": ""{CashSales}"" },
                        { ""Type"": ""KeyValue"", ""Key"": ""(+) Dotación Morralla:"", ""ValuePlaceholder"": ""{Inflows}"" },
                        { ""Type"": ""KeyValue"", ""Key"": ""(-) Recolección Efect:"", ""ValuePlaceholder"": ""{Outflows}"" },
                        { ""Type"": ""KeyValue"", ""Key"": ""(-) Devolución Efect:"", ""ValuePlaceholder"": ""{Returns}"" },
                        { ""Type"": ""Separator"", ""SeparatorChar"": ""-"" },
                        { ""Type"": ""KeyValue"", ""Key"": ""(=) Efectivo Esperado:"", ""ValuePlaceholder"": ""{ExpectedCash}"" },
                        { ""Type"": ""KeyValue"", ""Key"": ""(=) Efectivo Físico:"", ""ValuePlaceholder"": ""{PhysicalCash}"" },
                        { ""Type"": ""KeyValue"", ""Key"": ""DIFERENCIA ("", ""ValuePlaceholder"": ""{DiffStatus}) : {Difference}"" },
                        { ""Type"": ""Separator"", ""SeparatorChar"": ""="" },
                        { ""Type"": ""Text"", ""Content"": ""_____________________     _____________________\nFirma de Cajero          Firma de Auditor"", ""Align"": ""Center"" }
                    ]
                }";

            case TicketTemplateType.Order:
                return @"{
                    ""Blocks"": [
                        { ""Type"": ""Logo"" },
                        { ""Type"": ""Text"", ""Content"": ""--- COMPROBANTE DE PEDIDO ---"", ""Align"": ""Center"", ""Bold"": true },
                        { ""Type"": ""KeyValue"", ""Key"": ""Folio:"", ""ValuePlaceholder"": ""{Folio}"" },
                        { ""Type"": ""KeyValue"", ""Key"": ""Fecha:"", ""ValuePlaceholder"": ""{Date}"" },
                        { ""Type"": ""Separator"", ""SeparatorChar"": ""="" },
                        { ""Type"": ""Text"", ""Content"": ""CLIENTE Y ENTREGA:"", ""Align"": ""Left"", ""Bold"": true },
                        { ""Type"": ""Text"", ""Content"": ""Nombre: {ClientName}\\nTel: {ClientPhone}\\nDirecc: {ClientAddress}"", ""Align"": ""Left"" },
                        { ""Type"": ""KeyValue"", ""Key"": ""Zona:"", ""ValuePlaceholder"": ""{DeliveryZoneName}"" },
                        { ""Type"": ""KeyValue"", ""Key"": ""Envio:"", ""ValuePlaceholder"": ""{DeliveryCost}"" },
                        { ""Type"": ""KeyValue"", ""Key"": ""Método Pago:"", ""ValuePlaceholder"": ""{PaymentMethod}"" },
                        { ""Type"": ""Separator"", ""SeparatorChar"": ""-"" },
                        { ""Type"": ""ItemsTable"", ""Columns"": [
                            { ""Field"": ""Name"", ""Title"": ""Producto"", ""WidthPercentage"": 50 },
                            { ""Field"": ""Quantity"", ""Title"": ""Cant"", ""WidthPercentage"": 15 },
                            { ""Field"": ""Price"", ""Title"": ""Precio"", ""WidthPercentage"": 15 },
                            { ""Field"": ""Total"", ""Title"": ""Total"", ""WidthPercentage"": 20 }
                        ], ""WrapText"": true },
                        { ""Type"": ""Totals"" },
                        { ""Type"": ""Separator"", ""SeparatorChar"": ""="" },
                        { ""Type"": ""Footer"", ""Content"": ""¡Gracias por su compra!"" }
                    ]
                }";

            case TicketTemplateType.RouteManifest:
                return @"{
                    ""Blocks"": [
                        { ""Type"": ""Text"", ""Content"": ""=== MANIFIESTO DE REPARTO ==="", ""Align"": ""Center"", ""Bold"": true },
                        { ""Type"": ""KeyValue"", ""Key"": ""Ruta Folio:"", ""ValuePlaceholder"": ""{Folio}"" },
                        { ""Type"": ""KeyValue"", ""Key"": ""Fecha:"", ""ValuePlaceholder"": ""{Date}"" },
                        { ""Type"": ""KeyValue"", ""Key"": ""Repartidor:"", ""ValuePlaceholder"": ""{DeliveryManName}"" },
                        { ""Type"": ""Separator"", ""SeparatorChar"": ""="" },
                        { ""Type"": ""Text"", ""Content"": ""PEDIDOS EN RUTA:"", ""Align"": ""Left"", ""Bold"": true },
                        { ""Type"": ""Text"", ""Content"": ""{OrdersList}"", ""Align"": ""Left"" },
                        { ""Type"": ""Text"", ""Content"": ""RESUMEN DE ARQUEO A ENTREGAR:"", ""Align"": ""Left"", ""Bold"": true },
                        { ""Type"": ""Separator"", ""SeparatorChar"": ""="" },
                        { ""Type"": ""KeyValue"", ""Key"": ""Efectivo Esperado:"", ""ValuePlaceholder"": ""{ExpectedCash}"" },
                        { ""Type"": ""KeyValue"", ""Key"": ""Vouchers Esperados:"", ""ValuePlaceholder"": ""{ExpectedCard}"" },
                        { ""Type"": ""Separator"", ""SeparatorChar"": ""-"" },
                        { ""Type"": ""KeyValue"", ""Key"": ""MONTO TOTAL:"", ""ValuePlaceholder"": ""{Total}"", ""Bold"": true },
                        { ""Type"": ""Separator"", ""SeparatorChar"": ""="" },
                        { ""Type"": ""Text"", ""Content"": ""_________________________\nFirma Repartidor"", ""Align"": ""Center"" }
                    ]
                }";

            default:
                return "{\"Blocks\":[]}";
        }
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

    private async Task<string> ProcessLogoPlaceholderAsync(string ticketText, CancellationToken cancellationToken)
    {
        if (!ticketText.Contains("[LOGO]", StringComparison.OrdinalIgnoreCase))
        {
            return ticketText;
        }

        var logoEntity = await _context.Logos
            .AsNoTracking()
            .FirstOrDefaultAsync(l => l.Purpose == LogoPurpose.Ticket, cancellationToken);

        string logoTag = logoEntity != null ? $"[LOGO:{Convert.ToBase64String(logoEntity.Data)}]" : "";
        return ticketText.Replace("[LOGO]", logoTag, StringComparison.OrdinalIgnoreCase);
    }
}
