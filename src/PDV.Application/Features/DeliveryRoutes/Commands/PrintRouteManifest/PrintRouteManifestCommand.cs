using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using PDV.Application.Common.Interfaces;
using PDV.Domain.Enums;

namespace PDV.Application.Features.DeliveryRoutes.Commands.PrintRouteManifest;

public record PrintRouteManifestCommand(Guid RouteId, Guid CashRegisterId) : IRequest;

public class PrintRouteManifestCommandHandler : IRequestHandler<PrintRouteManifestCommand>
{
    private readonly IEscPosPrinter _escPosPrinter;
    private readonly IApplicationDbContext _context;
    private readonly ITicketGenerator _ticketGenerator;

    public PrintRouteManifestCommandHandler(
        IEscPosPrinter escPosPrinter,
        IApplicationDbContext context,
        ITicketGenerator ticketGenerator)
    {
        _escPosPrinter = escPosPrinter;
        _context = context;
        _ticketGenerator = ticketGenerator;
    }

    public async Task Handle(PrintRouteManifestCommand request, CancellationToken cancellationToken)
    {
        var cashRegister = await _context.CashRegisters.FindAsync(new object[] { request.CashRegisterId }, cancellationToken);
        if (cashRegister == null || !cashRegister.AssignedPrinterId.HasValue) return;

        var printer = await _context.Printers.FindAsync(new object[] { cashRegister.AssignedPrinterId.Value }, cancellationToken);
        if (printer == null) return;

        string ticketText = await _ticketGenerator.GenerateRouteManifestTicketAsync(request.RouteId, cancellationToken);

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
                ticketText,
                encodingCodePage: printer.CodePage > 0 ? printer.CodePage : 28591,
                cancellationToken: cancellationToken
            );
        }
        catch
        {
            // Fallo silencioso
        }
    }
}
