using MediatR;
using PDV.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;
using PDV.Domain.Enums;

namespace PDV.Application.Features.Sales.Commands.CashCut;

public record PrintCashCutTicketCommand(Guid CutId, Guid CashRegisterId) : IRequest;

public class PrintCashCutTicketCommandHandler : IRequestHandler<PrintCashCutTicketCommand>
{
    private readonly ITicketGenerator _ticketGenerator;
    private readonly IEscPosPrinter _escPosPrinter;
    private readonly IApplicationDbContext _context;

    public PrintCashCutTicketCommandHandler(
        ITicketGenerator ticketGenerator,
        IEscPosPrinter escPosPrinter,
        IApplicationDbContext context)
    {
        _ticketGenerator = ticketGenerator;
        _escPosPrinter = escPosPrinter;
        _context = context;
    }

    public async Task Handle(PrintCashCutTicketCommand request, CancellationToken cancellationToken)
    {
        // Obtener caja registradora
        var cashRegister = await _context.CashRegisters.FindAsync(new object[] { request.CashRegisterId }, cancellationToken)
            ?? throw new KeyNotFoundException($"Caja registradora {request.CashRegisterId} no encontrada");

        // Verificar que tenga impresora asignada
        if (!cashRegister.AssignedPrinterId.HasValue)
        {
            return;
        }

        // Obtener impresora
        var printer = await _context.Printers.FindAsync(new object[] { cashRegister.AssignedPrinterId.Value }, cancellationToken);
        if (printer == null)
        {
            return;
        }

        // Formatear conexión de impresora local o red
        string connectionUri = printer.ConnectionType switch
        {
            PrinterConnectionType.Network => printer.IpAddress ?? string.Empty,
            PrinterConnectionType.Usb => $"usb://{printer.DevicePath}",
            PrinterConnectionType.Serial => $"serial://{printer.DevicePath}?baud={printer.CodePage}",
            _ => printer.IpAddress ?? string.Empty
        };

        // Generar contenido del ticket
        var ticketContent = await _ticketGenerator.GenerateCashCutTicketAsync(request.CutId, cancellationToken);

        // Imprimir
        try
        {
            await _escPosPrinter.PrintTextAsync(
                connectionUri,
                printer.Port ?? 9100,
                ticketContent,
                encodingCodePage: printer.CodePage > 0 ? printer.CodePage : 28591, // Codepágina configurada o Latin-1
                cancellationToken: cancellationToken
            );
        }
        catch (Exception)
        {
            // Silencioso
            return;
        }
    }
}
