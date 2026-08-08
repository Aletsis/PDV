using System;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PDV.Application.Common.Interfaces;
using PDV.Domain.Enums;
using PDV.Domain.Repositories;

namespace PDV.Application.Features.DeliveryRoutes.Commands.PrintRouteManifest;

public record PrintRouteManifestCommand(Guid RouteId, Guid CashRegisterId) : IRequest;

public class PrintRouteManifestCommandHandler : IRequestHandler<PrintRouteManifestCommand>
{
    private readonly IDeliveryRouteRepository _routeRepository;
    private readonly IEscPosPrinter _escPosPrinter;
    private readonly IIdentityService _identityService;
    private readonly IApplicationDbContext _context;

    public PrintRouteManifestCommandHandler(
        IDeliveryRouteRepository routeRepository,
        IEscPosPrinter escPosPrinter,
        IIdentityService identityService,
        IApplicationDbContext context)
    {
        _routeRepository = routeRepository;
        _escPosPrinter = escPosPrinter;
        _identityService = identityService;
        _context = context;
    }

    public async Task Handle(PrintRouteManifestCommand request, CancellationToken cancellationToken)
    {
        var route = await _routeRepository.GetByIdWithOrdersAsync(request.RouteId, cancellationToken);
        if (route == null) return;

        var deliveryMan = await _identityService.GetUserByIdAsync(route.DeliveryManId, cancellationToken);

        var cashRegister = await _context.CashRegisters.FindAsync(new object[] { request.CashRegisterId }, cancellationToken);
        if (cashRegister == null || !cashRegister.AssignedPrinterId.HasValue) return;

        var printer = await _context.Printers.FindAsync(new object[] { cashRegister.AssignedPrinterId.Value }, cancellationToken);
        if (printer == null) return;

        var config = await _context.SystemConfigurations.FirstOrDefaultAsync(cancellationToken);
        int width = config?.TicketWidth ?? 40;

        var sb = new StringBuilder();

        // Encabezado
        sb.AppendLine(Center("=== MANIFIESTO DE REPARTO ===", width));
        sb.AppendLine(Center($"Ruta Folio: {route.Folio}", width));
        sb.AppendLine(Center($"Fecha: {route.CreatedDate.ToLocalTime():dd/MM/yyyy HH:mm}", width));
        sb.AppendLine(Center($"Repartidor: {deliveryMan?.FullName ?? route.DeliveryManId}", width));
        sb.AppendLine(new string('=', width));

        decimal totalCash = 0;
        decimal totalCard = 0;
        int orderCount = 0;

        sb.AppendLine("PEDIDOS EN RUTA:");
        sb.AppendLine(new string('-', width));

        foreach (var order in route.Orders)
        {
            orderCount++;
            var client = order.ClientId.HasValue
                ? await _context.Clients.FindAsync(new object[] { order.ClientId.Value }, cancellationToken)
                : null;

            sb.AppendLine($"#{orderCount} Pedido: {order.Series}-{order.Folio}");
            sb.AppendLine($"Cliente: {client?.Name ?? "Público General"}");
            sb.AppendLine($"Direcc:  {client?.Address?.Street ?? "Sin dirección"}");
            
            string payMethodStr = order.PaymentMethod == PaymentMethodType.Cash ? "Efectivo" : "Tarjeta";
            sb.AppendLine($"Cobro:   {order.TotalAmount:C2} ({payMethodStr})");
            sb.AppendLine(new string('-', width));

            if (order.PaymentMethod == PaymentMethodType.Cash)
            {
                totalCash += order.TotalAmount;
            }
            else
            {
                totalCard += order.TotalAmount;
            }
        }

        // Resumen de Arqueo
        sb.AppendLine();
        sb.AppendLine("RESUMEN DE ARQUEO A ENTREGAR:");
        sb.AppendLine(new string('=', width));
        sb.AppendLine($"Efectivo Esperado: {totalCash:C2}");
        sb.AppendLine($"Vouchers Esperados: {totalCard:C2}");
        sb.AppendLine(new string('-', width));
        sb.AppendLine($"MONTO TOTAL:       {(totalCash + totalCard):C2}");
        sb.AppendLine();
        sb.AppendLine(Center("Firma Repartidor", width));
        sb.AppendLine("\n\n");
        sb.AppendLine(Center("_________________________", width));
        sb.AppendLine("\n\n\n\n\n");

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
            // Fallo silencioso
        }
    }

    private static string Center(string text, int width)
    {
        if (text.Length >= width) return text.Substring(0, width);
        int spaces = (width - text.Length) / 2;
        return new string(' ', spaces) + text;
    }
}
