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
                .ThenInclude(i => i.Product)
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

        var folioText = string.IsNullOrEmpty(sale.Series) ? sale.Folio.ToString() : $"{sale.Series}{sale.Folio}";

        var variables = new Dictionary<string, string>
        {
            { "{CompanyName}", config?.CompanyName ?? string.Empty },
            { "{TaxId}", config?.TaxId ?? string.Empty },
            { "{BranchName}", sale.Branch?.Name ?? string.Empty },
            { "{BranchAddress}", FormatAddress(sale.Branch?.Address) },
            { "{BranchPhone}", sale.Branch?.Phone ?? string.Empty },
            { "{Folio}", folioText },
            { "{Id}", sale.SaleNumber },
            { "{Date}", sale.Date.ToLocalTime().ToString("dd/MM/yyyy HH:mm") },
            { "{CashRegisterName}", sale.CashRegister?.Name ?? string.Empty },
            { "{UserFullName}", user?.FullName ?? sale.UserId ?? string.Empty },
            { "{ClientName}", sale.Client?.Name ?? "Público General" },
            { "{ClientAddress}", FormatAddress(sale.Client?.Address) },
            { "{ClientPhone}", sale.Client?.Phone ?? string.Empty },
            { "{Subtotal}", sale.Subtotal.ToString("C2") },
            { "{Tax}", sale.Taxes.Sum(t => t.TaxAmount).ToString("C2") },
            { "{Total}", sale.TotalAmount.ToString("C2") },
            { "{PaymentMethod}", GetPaymentMethodTranslation(sale.PaymentMethod) },
            { "{Change}", sale.Change.ToString("C2") }
        };

        var tableItems = sale.Items.Select(item => new TicketTableItem
        {
            Name = item.ProductName,
            Code = item.Product?.Code ?? string.Empty,
            Quantity = item.Quantity.ToString("0.##"),
            Price = item.UnitPrice.ToString("C2"),
            PriceSinIva = item.UnitPrice.ToString("C2"),
            PriceConIva = (item.UnitPrice * (1 + (item.IsTaxExempt ? 0m : item.TaxRate / 100m))).ToString("C2"),
            Subtotal = item.Subtotal.ToString("C2"),
            Iva = item.TotalTax.ToString("C2"),
            Total = item.TotalAmount.ToString("C2")
        }).ToList();

        var templateJson = await _context.TicketTemplates
            .FirstOrDefaultAsync(t => t.TemplateType == TicketTemplateType.Sale && t.IsDefault, cancellationToken);
        string jsonStr = templateJson?.ContentJson ?? GetDefaultTemplateJson(TicketTemplateType.Sale);
        var template = JsonSerializer.Deserialize<TicketTemplateJson>(jsonStr, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new TicketTemplateJson();

        var ticketText = DynamicTicketRenderer.Render(template, variables, tableItems, width);
        ticketText = await ProcessLogoPlaceholderAsync(ticketText, cancellationToken);
        return ticketText;
    }

    public async Task<string> GenerateInvoiceTicketAsync(Guid invoiceId, CancellationToken cancellationToken = default, int? widthCharacters = null)
    {
        var invoice = await _context.Invoices
            .Include(i => i.Branch)
                .ThenInclude(b => b!.Address)
            .Include(i => i.Client)
            .Include(i => i.Sale)
                .ThenInclude(s => s!.Items)
                    .ThenInclude(i => i.Product)
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
                Code = item.Product?.Code ?? string.Empty,
                Quantity = item.Quantity.ToString("0.##"),
                Price = item.UnitPrice.ToString("C2"),
                PriceSinIva = item.UnitPrice.ToString("C2"),
                PriceConIva = (item.UnitPrice * (1 + (item.IsTaxExempt ? 0m : item.TaxRate / 100m))).ToString("C2"),
                Subtotal = item.Subtotal.ToString("C2"),
                Iva = item.TotalTax.ToString("C2"),
                Total = item.TotalAmount.ToString("C2")
            }).ToList();
        }
        else
        {
            tableItems.Add(new TicketTableItem
            {
                Name = "CONSOLIDADO GLOBAL DE VENTAS",
                Code = string.Empty,
                Quantity = "1",
                Price = invoice.Subtotal.ToString("C2"),
                PriceSinIva = invoice.Subtotal.ToString("C2"),
                PriceConIva = invoice.Total.ToString("C2"),
                Subtotal = invoice.Subtotal.ToString("C2"),
                Iva = invoice.Tax.ToString("C2"),
                Total = invoice.Total.ToString("C2")
            });
        }

        var templateJson = await _context.TicketTemplates
            .FirstOrDefaultAsync(t => t.TemplateType == TicketTemplateType.Invoice && t.IsDefault, cancellationToken);
        string jsonStr = templateJson?.ContentJson ?? GetDefaultTemplateJson(TicketTemplateType.Invoice);
        var template = JsonSerializer.Deserialize<TicketTemplateJson>(jsonStr, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new TicketTemplateJson();

        var ticketText = DynamicTicketRenderer.Render(template, variables, tableItems, width);
        ticketText = await ProcessLogoPlaceholderAsync(ticketText, cancellationToken);
        return ticketText;
    }

    public async Task<string> GenerateReturnTicketAsync(Guid returnId, CancellationToken cancellationToken = default, int? widthCharacters = null)
    {
        var returnSale = await _context.Returns
            .Include(r => r.Items)
                .ThenInclude(i => i.Product)
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
            { "{BranchAddress}", FormatAddress(returnSale.Branch?.Address) },
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
            Code = item.Product?.Code ?? string.Empty,
            Quantity = item.Quantity.ToString("0.##"),
            Price = item.UnitPrice.ToString("C2"),
            PriceSinIva = item.UnitPrice.ToString("C2"),
            PriceConIva = (item.UnitPrice * (1 + (item.IsTaxExempt ? 0m : item.TaxRate / 100m))).ToString("C2"),
            Subtotal = item.Subtotal.ToString("C2"),
            Iva = item.TotalTax.ToString("C2"),
            Total = item.TotalAmount.ToString("C2")
        }).ToList();

        var templateJson = await _context.TicketTemplates
            .FirstOrDefaultAsync(t => t.TemplateType == TicketTemplateType.Return && t.IsDefault, cancellationToken);
        string jsonStr = templateJson?.ContentJson ?? GetDefaultTemplateJson(TicketTemplateType.Return);
        var template = JsonSerializer.Deserialize<TicketTemplateJson>(jsonStr, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new TicketTemplateJson();

        var ticketText = DynamicTicketRenderer.Render(template, variables, tableItems, width);
        ticketText = await ProcessLogoPlaceholderAsync(ticketText, cancellationToken);
        return ticketText;
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

        bool isInflow = collection.Type == CashCollectionType.Morralla;
        var ticketTitle = isInflow ? "DOTACIÓN DE MORRALLA" : "RECOLECCIÓN DE EFECTIVO";
        var cleanReason = collection.Reason;

        var variables = new Dictionary<string, string>
        {
            { "{CompanyName}", config?.CompanyName ?? string.Empty },
            { "{TaxId}", config?.TaxId ?? string.Empty },
            { "{BranchName}", collection.CashRegister?.Branch?.Name ?? string.Empty },
            { "{BranchAddress}", FormatAddress(collection.CashRegister?.Branch?.Address) },
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

        var tableItems = collection.Denominations
            .Select(d => {
                bool isBill = d.Type.ToString().StartsWith("Bill", StringComparison.OrdinalIgnoreCase);
                return new TicketTableItem
                {
                    Name = isBill ? $"Billete ${d.Type.GetValue():0}" : $"Moneda ${d.Type.GetValue():0.00}",
                    Quantity = d.Quantity.ToString(),
                    Total = d.TotalValue.ToString("C2")
                };
            })
            .ToList();

        var ticketText = DynamicTicketRenderer.Render(template, variables, tableItems, width);
        ticketText = await ProcessLogoPlaceholderAsync(ticketText, cancellationToken);
        return ticketText;
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
        var totalInflows = cashCollections.Where(c => c.Type == CashCollectionType.Morralla).Sum(c => c.Amount);
        var totalOutflows = cashCollections.Where(c => c.Type == CashCollectionType.Recoleccion).Sum(c => c.Amount);

        var sales = await _context.Sales
            .Include(s => s.Taxes)
            .Where(s => s.ShiftId == cut.ShiftId && s.IsPaid)
            .ToListAsync(cancellationToken);

        var salesTotal = sales.Sum(s => s.TotalAmount);
        var cashSalesTotal = sales.Where(s => s.PaymentMethod == PaymentMethodType.Cash).Sum(s => s.TotalAmount);
        var cardSalesTotal = sales.Where(s => s.PaymentMethod == PaymentMethodType.CreditCard || s.PaymentMethod == PaymentMethodType.DebitCard).Sum(s => s.TotalAmount);

        var salesTaxTotals = sales
            .SelectMany(s => s.Taxes)
            .GroupBy(t => new { t.Rate, t.IsExempt })
            .Select(g => new {
                Rate = g.Key.Rate,
                IsExempt = g.Key.IsExempt,
                BaseAmount = g.Sum(t => t.BaseAmount),
                TaxAmount = g.Sum(t => t.TaxAmount)
            })
            .ToList();

        var venta16 = salesTaxTotals.FirstOrDefault(t => t.Rate == 0.16m && !t.IsExempt)?.BaseAmount ?? 0m;
        var venta8 = salesTaxTotals.FirstOrDefault(t => t.Rate == 0.08m && !t.IsExempt)?.BaseAmount ?? 0m;
        var venta0 = salesTaxTotals.FirstOrDefault(t => t.Rate == 0.00m && !t.IsExempt)?.BaseAmount ?? 0m;
        var ventaExento = salesTaxTotals.FirstOrDefault(t => t.IsExempt)?.BaseAmount ?? 0m;

        var iva16 = salesTaxTotals.FirstOrDefault(t => t.Rate == 0.16m && !t.IsExempt)?.TaxAmount ?? 0m;
        var iva8 = salesTaxTotals.FirstOrDefault(t => t.Rate == 0.08m && !t.IsExempt)?.TaxAmount ?? 0m;
        var iva0 = salesTaxTotals.FirstOrDefault(t => t.Rate == 0.00m && !t.IsExempt)?.TaxAmount ?? 0m;

        var total16 = venta16 + iva16;
        var total8 = venta8 + iva8;
        var total0 = venta0 + iva0;

        var cashReturns = cut.Shift?.TotalCashReturns ?? 0m;

        string diffStatus = "CUADRADO";
        if (cut.Difference < 0) diffStatus = "FALTANTE";
        else if (cut.Difference > 0) diffStatus = "SOBRANTE";

        var variables = new Dictionary<string, string>
        {
            { "{CompanyName}", config?.CompanyName ?? string.Empty },
            { "{TaxId}", config?.TaxId ?? string.Empty },
            { "{BranchName}", cut.CashRegister?.Branch?.Name ?? string.Empty },
            { "{BranchAddress}", FormatAddress(cut.CashRegister?.Branch?.Address) },
            { "{Folio}", cut.Id.ToString().Substring(0, 8).ToUpperInvariant() },
            { "{Date}", cut.CutDate.ToLocalTime().ToString("dd/MM/yyyy HH:mm") },
            { "{CashRegisterName}", cut.CashRegister?.Name ?? string.Empty },
            { "{UserFullName}", user?.FullName ?? cut.UserId },
            { "{InitialCash}", initialCash.ToString("C2") },
            { "{CashSales}", cashSalesTotal.ToString("C2") },
            { "{Inflows}", totalInflows.ToString("C2") },
            { "{Outflows}", totalOutflows.ToString("C2") },
            { "{Returns}", cashReturns.ToString("C2") },
            { "{ExpectedCash}", cut.SystemExpectedCash.ToString("C2") },
            { "{PhysicalCash}", cut.DeclaredPhysicalCash.ToString("C2") },
            { "{DiffStatus}", diffStatus },
            { "{Difference}", cut.Difference.ToString("C2") },

            // Spanish variables
            { "{Fondo}", initialCash.ToString("C2") },
            { "{Morralla}", totalInflows.ToString("C2") },
            { "{Recolecciones}", totalOutflows.ToString("C2") },
            { "{VentaTotal}", salesTotal.ToString("C2") },
            { "{VentaEfectivo}", cashSalesTotal.ToString("C2") },
            { "{VentaTarjeta}", cardSalesTotal.ToString("C2") },
            { "{Venta16}", venta16.ToString("C2") },
            { "{Venta8}", venta8.ToString("C2") },
            { "{Venta0}", venta0.ToString("C2") },
            { "{VentaExento}", ventaExento.ToString("C2") },
            { "{VentaExcento}", ventaExento.ToString("C2") },
            { "{Iva16}", iva16.ToString("C2") },
            { "{Iva8}", iva8.ToString("C2") },
            { "{Iva0}", iva0.ToString("C2") },
            { "{Total16}", total16.ToString("C2") },
            { "{Total8}", total8.ToString("C2") },
            { "{Total0}", total0.ToString("C2") },
            { "{EfectivoEsperado}", cut.SystemExpectedCash.ToString("C2") }
        };

        var templateJson = await _context.TicketTemplates
            .FirstOrDefaultAsync(t => t.TemplateType == TicketTemplateType.CashCut && t.IsDefault, cancellationToken);
        string jsonStr = templateJson?.ContentJson ?? GetDefaultTemplateJson(TicketTemplateType.CashCut);
        var template = JsonSerializer.Deserialize<TicketTemplateJson>(jsonStr, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new TicketTemplateJson();

        var ticketText = DynamicTicketRenderer.Render(template, variables, new List<TicketTableItem>(), width);
        ticketText = await ProcessLogoPlaceholderAsync(ticketText, cancellationToken);
        return ticketText;
    }

    public async Task<string> GenerateOrderTicketAsync(Guid orderId, CancellationToken cancellationToken = default, int? widthCharacters = null)
    {
        var order = await _context.Orders
            .Include(o => o.Items)
                .ThenInclude(i => i.Product)
            .Include(o => o.Branch)
                .ThenInclude(b => b!.Address)
            .Include(o => o.CashRegister)
            .FirstOrDefaultAsync(o => o.Id == orderId, cancellationToken)
            ?? throw new KeyNotFoundException($"Pedido {orderId} no encontrado");

        var client = order.ClientId.HasValue
            ? await _context.Clients.Include(c => c.DeliveryZone).FirstOrDefaultAsync(c => c.Id == order.ClientId.Value, cancellationToken)
            : null;

        var config = await _context.SystemConfigurations.FirstOrDefaultAsync(cancellationToken);
        var width = widthCharacters ?? 42;

        var cashierUserId = !string.IsNullOrEmpty(order.CapturedById) ? order.CapturedById : order.TakenById;
        var user = !string.IsNullOrEmpty(cashierUserId)
            ? await _context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == cashierUserId, cancellationToken)
            : null;

        decimal deliveryCost = client?.DeliveryZone?.DeliveryCost ?? 0m;

        string channelText = order.Channel switch
        {
            OrderChannel.Telephone => "Teléfono",
            OrderChannel.WhatsApp => "WhatsApp",
            OrderChannel.Store => "Mostrador",
            OrderChannel.Web => "Web / En Línea",
            OrderChannel.MobileApp => "App Móvil",
            _ => "Otro"
        };

        var variables = new Dictionary<string, string>
        {
            { "{CompanyName}", config?.CompanyName ?? string.Empty },
            { "{TaxId}", config?.TaxId ?? string.Empty },
            { "{BranchName}", order.Branch?.Name ?? string.Empty },
            { "{BranchAddress}", FormatAddress(order.Branch?.Address) },
            { "{BranchPhone}", order.Branch?.Phone ?? string.Empty },
            { "{Folio}", $"{order.Series}-{order.Folio}" },
            { "{Channel}", channelText },
            { "{Canal}", channelText },
            { "{Date}", order.OrderDate.ToLocalTime().ToString("dd/MM/yyyy HH:mm") },
            { "{CashRegisterName}", order.CashRegister?.Name ?? string.Empty },
            { "{UserFullName}", user?.FullName ?? cashierUserId ?? string.Empty },
            { "{ClientName}", client?.Name ?? "Público General" },
            { "{ClientPhone}", client?.Phone ?? string.Empty },
            { "{ClientAddress}", client?.Address != null ? FormatAddress(client.Address) : "Sin dirección" },
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
            Code = item.Product?.Code ?? string.Empty,
            Quantity = item.Quantity.ToString("0.##"),
            Price = item.UnitPrice.ToString("C2"),
            PriceSinIva = item.UnitPrice.ToString("C2"),
            PriceConIva = (item.UnitPrice * (1 + (item.IsTaxExempt ? 0m : item.TaxRate / 100m))).ToString("C2"),
            Subtotal = item.Subtotal.ToString("C2"),
            Iva = item.TotalTax.ToString("C2"),
            Total = item.TotalAmount.ToString("C2")
        }).ToList();

        var templateJson = await _context.TicketTemplates
            .FirstOrDefaultAsync(t => t.TemplateType == TicketTemplateType.Order && t.IsDefault, cancellationToken);
        string jsonStr = templateJson?.ContentJson ?? GetDefaultTemplateJson(TicketTemplateType.Order);
        var template = JsonSerializer.Deserialize<TicketTemplateJson>(jsonStr, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new TicketTemplateJson();

        var ticketText = DynamicTicketRenderer.Render(template, variables, tableItems, width);
        ticketText = await ProcessLogoPlaceholderAsync(ticketText, cancellationToken);
        return ticketText;
    }

    public async Task<string> GenerateRouteManifestTicketAsync(Guid routeId, CancellationToken cancellationToken = default, int? widthCharacters = null)
    {
        var route = await _context.DeliveryRoutes
            .Include(r => r.Branch)
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
        var manifestOrders = new List<ManifestOrderInfo>();

        foreach (var order in route.Orders)
        {
            orderCount++;
            var client = order.ClientId.HasValue
                ? await _context.Clients.FindAsync(new object[] { order.ClientId.Value }, cancellationToken)
                : null;

            string payMethodStr = order.PaymentMethod == PaymentMethodType.Cash ? "Efectivo" : "Tarjeta";

            manifestOrders.Add(new ManifestOrderInfo
            {
                Folio = $"{order.Series}-{order.Folio}",
                Client = client?.Name ?? "Público General",
                Address = client?.Address != null ? FormatAddress(client.Address) : "Sin dirección",
                Phone = client?.Phone ?? string.Empty,
                Total = order.TotalAmount,
                PaymentMethod = payMethodStr
            });

            ordersSb.AppendLine($"#{orderCount} Pedido: {order.Series}-{order.Folio}");
            ordersSb.AppendLine($"Cliente: {client?.Name ?? "Público General"}");
            ordersSb.AppendLine($"Direcc:  {(client?.Address != null ? FormatAddress(client.Address) : "Sin dirección")}");
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
            { "{BranchName}", route.Branch?.Name ?? string.Empty },
            { "{BranchAddress}", FormatAddress(route.Branch?.Address) },
            { "{BranchPhone}", route.Branch?.Phone ?? string.Empty },
            { "{Folio}", route.Folio.ToString() },
            { "{Date}", route.CreatedDate.ToLocalTime().ToString("dd/MM/yyyy HH:mm") },
            { "{DeliveryManName}", deliveryMan?.FullName ?? route.DeliveryManId },
            { "{OrdersList}", ordersSb.ToString() },
            { "{ExpectedCash}", totalCash.ToString("C2") },
            { "{ExpectedCard}", totalCard.ToString("C2") },
            { "{OrderCount}", orderCount.ToString() },
            { "{Total}", (totalCash + totalCard).ToString("C2") }
        };

        var templateJson = await _context.TicketTemplates
            .FirstOrDefaultAsync(t => t.TemplateType == TicketTemplateType.RouteManifest && t.IsDefault, cancellationToken);
        string jsonStr = templateJson?.ContentJson ?? GetDefaultTemplateJson(TicketTemplateType.RouteManifest);
        var template = JsonSerializer.Deserialize<TicketTemplateJson>(jsonStr, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new TicketTemplateJson();

        var ticketText = DynamicTicketRenderer.Render(template, variables, new List<TicketTableItem>(), width, manifestOrders);
        ticketText = await ProcessLogoPlaceholderAsync(ticketText, cancellationToken);
        return ticketText;
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
                        { ""Type"": ""Totals"", ""TotalsFields"": [""Subtotal"", ""Iva"", ""Total"", ""PaymentMethod"", ""Change""] },
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
                        { ""Type"": ""Totals"", ""TotalsFields"": [""Subtotal"", ""Iva"", ""Total"", ""PaymentMethod"", ""Change""] },
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
                        { ""Type"": ""Totals"", ""TotalsFields"": [""Subtotal"", ""Iva"", ""Total"", ""PaymentMethod"", ""Change""] },
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
                        { ""Type"": ""DenominationsTable"", ""Columns"": [
                            { ""Field"": ""Name"", ""Title"": ""Denominación"", ""WidthPercentage"": 50 },
                            { ""Field"": ""Quantity"", ""Title"": ""Cant"", ""WidthPercentage"": 20 },
                            { ""Field"": ""Total"", ""Title"": ""Total"", ""WidthPercentage"": 30 }
                        ], ""WrapText"": true },
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
                        { ""Type"": ""Text"", ""Content"": ""CORTE DE CAJA"", ""Align"": ""Center"", ""Bold"": true },
                        { ""Type"": ""Separator"", ""SeparatorChar"": ""="" },
                        { ""Type"": ""KeyValue"", ""Key"": ""Folio Corte:"", ""ValuePlaceholder"": ""{Folio}"" },
                        { ""Type"": ""KeyValue"", ""Key"": ""Fecha Corte:"", ""ValuePlaceholder"": ""{Date}"" },
                        { ""Type"": ""KeyValue"", ""Key"": ""Caja:"", ""ValuePlaceholder"": ""{CashRegisterName}"" },
                        { ""Type"": ""KeyValue"", ""Key"": ""Cajero:"", ""ValuePlaceholder"": ""{UserFullName}"" },
                        { ""Type"": ""Separator"", ""SeparatorChar"": ""-"" },
                        { ""Type"": ""Text"", ""Content"": ""RESUMEN DE EFECTIVO"", ""Align"": ""Center"", ""Bold"": true },
                        { ""Type"": ""Separator"", ""SeparatorChar"": ""-"" },
                        { ""Type"": ""KeyValue"", ""Key"": ""Fondo Inicial:"", ""ValuePlaceholder"": ""{Fondo}"" },
                        { ""Type"": ""KeyValue"", ""Key"": ""(+) Venta Efectivo:"", ""ValuePlaceholder"": ""{VentaEfectivo}"" },
                        { ""Type"": ""KeyValue"", ""Key"": ""(+) Morralla:"", ""ValuePlaceholder"": ""{Morralla}"" },
                        { ""Type"": ""KeyValue"", ""Key"": ""(-) Recolecciones:"", ""ValuePlaceholder"": ""{Recolecciones}"" },
                        { ""Type"": ""KeyValue"", ""Key"": ""(-) Devoluciones:"", ""ValuePlaceholder"": ""{Returns}"" },
                        { ""Type"": ""Separator"", ""SeparatorChar"": ""-"" },
                        { ""Type"": ""KeyValue"", ""Key"": ""Efectivo Esperado:"", ""ValuePlaceholder"": ""{EfectivoEsperado}"", ""Bold"": true },
                        { ""Type"": ""KeyValue"", ""Key"": ""Efectivo Físico:"", ""ValuePlaceholder"": ""{PhysicalCash}"" },
                        { ""Type"": ""KeyValue"", ""Key"": ""Diferencia:"", ""ValuePlaceholder"": ""({DiffStatus}) {Difference}"" },
                        { ""Type"": ""Separator"", ""SeparatorChar"": ""-"" },
                        { ""Type"": ""Text"", ""Content"": ""VENTAS POR METODO DE PAGO"", ""Align"": ""Center"", ""Bold"": true },
                        { ""Type"": ""Separator"", ""SeparatorChar"": ""-"" },
                        { ""Type"": ""KeyValue"", ""Key"": ""Venta Efectivo:"", ""ValuePlaceholder"": ""{VentaEfectivo}"" },
                        { ""Type"": ""KeyValue"", ""Key"": ""Venta Tarjeta:"", ""ValuePlaceholder"": ""{VentaTarjeta}"" },
                        { ""Type"": ""KeyValue"", ""Key"": ""Venta Total:"", ""ValuePlaceholder"": ""{VentaTotal}"", ""Bold"": true },
                        { ""Type"": ""Separator"", ""SeparatorChar"": ""-"" },
                        { ""Type"": ""Text"", ""Content"": ""DESGLOSE DE IMPUESTOS"", ""Align"": ""Center"", ""Bold"": true },
                        { ""Type"": ""Separator"", ""SeparatorChar"": ""-"" },
                        { ""Type"": ""KeyValue"", ""Key"": ""Venta 16%:"", ""ValuePlaceholder"": ""{Venta16}"" },
                        { ""Type"": ""KeyValue"", ""Key"": ""Iva 16%:"", ""ValuePlaceholder"": ""{Iva16}"" },
                        { ""Type"": ""KeyValue"", ""Key"": ""Total 16%:"", ""ValuePlaceholder"": ""{Total16}"" },
                        { ""Type"": ""Separator"", ""SeparatorChar"": ""."" },
                        { ""Type"": ""KeyValue"", ""Key"": ""Venta 8%:"", ""ValuePlaceholder"": ""{Venta8}"" },
                        { ""Type"": ""KeyValue"", ""Key"": ""Iva 8%:"", ""ValuePlaceholder"": ""{Iva8}"" },
                        { ""Type"": ""KeyValue"", ""Key"": ""Total 8%:"", ""ValuePlaceholder"": ""{Total8}"" },
                        { ""Type"": ""Separator"", ""SeparatorChar"": ""."" },
                        { ""Type"": ""KeyValue"", ""Key"": ""Venta 0%:"", ""ValuePlaceholder"": ""{Venta0}"" },
                        { ""Type"": ""KeyValue"", ""Key"": ""Iva 0%:"", ""ValuePlaceholder"": ""{Iva0}"" },
                        { ""Type"": ""KeyValue"", ""Key"": ""Total 0%:"", ""ValuePlaceholder"": ""{Total0}"" },
                        { ""Type"": ""Separator"", ""SeparatorChar"": ""."" },
                        { ""Type"": ""KeyValue"", ""Key"": ""Venta Exenta:"", ""ValuePlaceholder"": ""{VentaExento}"" },
                        { ""Type"": ""Separator"", ""SeparatorChar"": ""="" },
                        { ""Type"": ""Text"", ""Content"": ""_____________________     _____________________\nFirma de Cajero          Firma de Auditor"", ""Align"": ""Center"" }
                    ]
                }";

            case TicketTemplateType.Order:
                return @"{
                    ""Blocks"": [
                        { ""Type"": ""Logo"" },
                        { ""Type"": ""Text"", ""Content"": ""{CompanyName}"", ""Align"": ""Center"", ""Bold"": true },
                        { ""Type"": ""Text"", ""Content"": ""SUCURSAL: {BranchName}"", ""Align"": ""Center"" },
                        { ""Type"": ""Text"", ""Content"": ""{BranchAddress}"", ""Align"": ""Center"" },
                        { ""Type"": ""Text"", ""Content"": ""TEL: {BranchPhone}"", ""Align"": ""Center"" },
                        { ""Type"": ""Separator"", ""SeparatorChar"": ""="" },
                        { ""Type"": ""Text"", ""Content"": ""COMPROBANTE DE PEDIDO"", ""Align"": ""Center"", ""Bold"": true },
                        { ""Type"": ""Separator"", ""SeparatorChar"": ""="" },
                        { ""Type"": ""KeyValue"", ""Key"": ""Folio:"", ""ValuePlaceholder"": ""{Folio}"" },
                        { ""Type"": ""KeyValue"", ""Key"": ""Fecha:"", ""ValuePlaceholder"": ""{Date}"" },
                        { ""Type"": ""KeyValue"", ""Key"": ""Caja:"", ""ValuePlaceholder"": ""{CashRegisterName}"" },
                        { ""Type"": ""KeyValue"", ""Key"": ""Cajero:"", ""ValuePlaceholder"": ""{UserFullName}"" },
                        { ""Type"": ""Separator"", ""SeparatorChar"": ""-"" },
                        { ""Type"": ""Text"", ""Content"": ""CLIENTE Y ENTREGA:"", ""Align"": ""Left"", ""Bold"": true },
                        { ""Type"": ""Text"", ""Content"": ""Nombre: {ClientName}\nTel: {ClientPhone}\nDirecc: {ClientAddress}"", ""Align"": ""Left"" },
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
                        { ""Type"": ""Totals"", ""TotalsFields"": [""Subtotal"", ""Iva"", ""Total"", ""PaymentMethod"", ""Change""] },
                        { ""Type"": ""Separator"", ""SeparatorChar"": ""="" },
                        { ""Type"": ""Footer"", ""Content"": ""¡Gracias por su compra!"" }
                     ]
                 }";

            case TicketTemplateType.RouteManifest:
                return @"{
                    ""Blocks"": [
                        { ""Type"": ""Text"", ""Content"": ""=== MANIFIESTO DE REPARTO ==="", ""Align"": ""Center"", ""Bold"": true },
                        { ""Type"": ""KeyValue"", ""Key"": ""Sucursal:"", ""ValuePlaceholder"": ""{BranchName}"" },
                        { ""Type"": ""KeyValue"", ""Key"": ""Dirección:"", ""ValuePlaceholder"": ""{BranchAddress}"" },
                        { ""Type"": ""KeyValue"", ""Key"": ""Teléfono:"", ""ValuePlaceholder"": ""{BranchPhone}"" },
                        { ""Type"": ""Separator"", ""SeparatorChar"": ""="" },
                        { ""Type"": ""KeyValue"", ""Key"": ""Ruta Folio:"", ""ValuePlaceholder"": ""{Folio}"" },
                        { ""Type"": ""KeyValue"", ""Key"": ""Fecha:"", ""ValuePlaceholder"": ""{Date}"" },
                        { ""Type"": ""KeyValue"", ""Key"": ""Repartidor:"", ""ValuePlaceholder"": ""{DeliveryManName}"" },
                        { ""Type"": ""Separator"", ""SeparatorChar"": ""="" },
                        { ""Type"": ""ManifestOrders"", ""ManifestOrderFields"": [""Folio"", ""Client"", ""Address"", ""Phone"", ""Total""] },
                        { ""Type"": ""Separator"", ""SeparatorChar"": ""="" },
                        { ""Type"": ""ManifestTotals"", ""ManifestTotalsFields"": [""CashTotal"", ""OrderCount"", ""CardTotal"", ""CombinedTotal""] },
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

    private string FormatAddress(Address? address)
    {
        if (address == null) return string.Empty;
        var parts = new List<string>();

        var streetPart = address.Street?.Trim() ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(address.ExteriorNumber))
            streetPart += $" #{address.ExteriorNumber.Trim()}";
        if (!string.IsNullOrWhiteSpace(address.InteriorNumber))
            streetPart += $" Int. {address.InteriorNumber.Trim()}";

        if (!string.IsNullOrWhiteSpace(streetPart))
            parts.Add(streetPart);

        if (!string.IsNullOrWhiteSpace(address.Colony))
            parts.Add($"Col. {address.Colony.Trim()}");

        if (!string.IsNullOrWhiteSpace(address.ZipCode) && address.ZipCode != "00000")
            parts.Add($"C.P. {address.ZipCode.Trim()}");

        return string.Join(", ", parts);
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
