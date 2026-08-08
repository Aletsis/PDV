using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PDV.Application.Common.Interfaces;
using PDV.Domain.Enums;
using PDV.Domain.Repositories;

namespace PDV.Application.Features.Orders.Commands.PrintOrderTicket;

public record PrintOrderTicketCommand(Guid OrderId, Guid CashRegisterId) : IRequest;

public class PrintOrderTicketCommandHandler : IRequestHandler<PrintOrderTicketCommand>
{
    private readonly IOrderRepository _orderRepository;
    private readonly IEscPosPrinter _escPosPrinter;
    private readonly IApplicationDbContext _context;

    public PrintOrderTicketCommandHandler(
        IOrderRepository orderRepository,
        IEscPosPrinter escPosPrinter,
        IApplicationDbContext context)
    {
        _orderRepository = orderRepository;
        _escPosPrinter = escPosPrinter;
        _context = context;
    }

    public async Task Handle(PrintOrderTicketCommand request, CancellationToken cancellationToken)
    {
        var order = await _orderRepository.GetByIdWithItemsAsync(request.OrderId, cancellationToken);
        if (order == null) return;

        var client = order.ClientId.HasValue
            ? await _context.Clients.Include(c => c.DeliveryZone).FirstOrDefaultAsync(c => c.Id == order.ClientId.Value, cancellationToken)
            : null;

        var cashRegister = await _context.CashRegisters.FindAsync(new object[] { request.CashRegisterId }, cancellationToken);
        if (cashRegister == null || !cashRegister.AssignedPrinterId.HasValue) return;

        var printer = await _context.Printers.FindAsync(new object[] { cashRegister.AssignedPrinterId.Value }, cancellationToken);
        if (printer == null) return;

        var config = await _context.SystemConfigurations.FirstOrDefaultAsync(cancellationToken);
        int width = config?.TicketWidth ?? 40; // Ancho por defecto

        var sb = new StringBuilder();

        // Encabezado
        sb.AppendLine(Center("--- COMPROBANTE DE PEDIDO ---", width));
        sb.AppendLine(Center($"Folio: {order.Series}-{order.Folio}", width));
        sb.AppendLine(Center($"Fecha: {order.OrderDate.ToLocalTime():dd/MM/yyyy HH:mm}", width));
        sb.AppendLine(new string('=', width));

        // Cliente y Entrega
        sb.AppendLine("CLIENTE Y ENTREGA:");
        if (client != null)
        {
            sb.AppendLine($"Nombre: {client.Name}");
            sb.AppendLine($"Tel: {client.Phone}");
            sb.AppendLine($"Direcc: {client.Address?.Street ?? "Sin dirección"}");
            if (client.DeliveryZone != null)
            {
                sb.AppendLine($"Zona: {client.DeliveryZone.Name}");
                sb.AppendLine($"Envio: {client.DeliveryZone.DeliveryCost:C2}");
            }
        }
        else
        {
            sb.AppendLine("Cliente: Público General");
        }
        sb.AppendLine($"Método Pago: {(order.PaymentMethod == PaymentMethodType.Cash ? "Efectivo" : "Tarjeta")}");
        sb.AppendLine(new string('-', width));

        // Artículos
        sb.AppendLine(FormatRow("ARTICULO", "CANT", "PRECIO", "TOTAL", width));
        sb.AppendLine(new string('-', width));

        foreach (var item in order.Items)
        {
            string name = item.ProductName.Length > 18 ? item.ProductName.Substring(0, 16) + ".." : item.ProductName;
            string qty = item.Quantity.ToString("G29");
            string price = item.UnitPrice.ToString("F2");
            string total = item.TotalAmount.ToString("F2");
            sb.AppendLine(FormatRow(name, qty, price, total, width));
        }

        sb.AppendLine(new string('-', width));

        // Totales
        sb.AppendLine($"Subtotal: {order.Subtotal:C2}");
        sb.AppendLine($"Impuesto: {order.TotalTax:C2}");
        decimal deliveryCost = client?.DeliveryZone?.DeliveryCost ?? 0m;
        sb.AppendLine($"Envio:    {deliveryCost:C2}");
        sb.AppendLine(new string('=', width));
        sb.AppendLine($"TOTAL:    {(order.TotalAmount + deliveryCost):C2}");
        sb.AppendLine();
        sb.AppendLine(Center("¡Gracias por su compra!", width));
        sb.AppendLine("\n\n\n\n\n"); // Espacio para corte de papel

        string connectionUri = printer.ConnectionType switch
        {
            PrinterConnectionType.Network => printer.IpAddress ?? string.Empty,
            PrinterConnectionType.Usb => $"usb://{printer.DevicePath}",
            PrinterConnectionType.Serial => $"serial://{printer.DevicePath}?baud={printer.CodePage}",
            _ => printer.IpAddress ?? string.Empty
        };

        try
        {
            await _escPosPrinter.PrintTextAsync(
                connectionUri,
                printer.Port ?? 9100,
                sb.ToString(),
                encodingCodePage: printer.CodePage > 0 ? printer.CodePage : 28591,
                cancellationToken: cancellationToken
            );
        }
        catch
        {
            // Falla de impresión silenciosa
        }
    }

    private static string Center(string text, int width)
    {
        if (text.Length >= width) return text.Substring(0, width);
        int spaces = (width - text.Length) / 2;
        return new string(' ', spaces) + text;
    }

    private static string FormatRow(string col1, string col2, string col3, string col4, int width)
    {
        // Anchos de columna dinámicos: col1 (45%), col2 (12%), col3 (21%), col4 (22%)
        int w1 = (int)(width * 0.45);
        int w2 = (int)(width * 0.12);
        int w3 = (int)(width * 0.21);
        int w4 = width - w1 - w2 - w3;

        string c1 = col1.PadRight(w1).Substring(0, w1);
        string c2 = col2.PadLeft(w2).Substring(0, w2);
        string c3 = col3.PadLeft(w3).Substring(0, w3);
        string c4 = col4.PadLeft(w4).Substring(0, w4);

        return c1 + c2 + c3 + c4;
    }
}
