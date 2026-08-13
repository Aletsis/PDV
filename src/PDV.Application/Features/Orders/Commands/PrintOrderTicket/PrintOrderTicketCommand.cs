using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PDV.Application.Common.Interfaces;
using PDV.Domain.Enums;

namespace PDV.Application.Features.Orders.Commands.PrintOrderTicket;

public record PrintOrderTicketCommand(Guid OrderId, Guid CashRegisterId) : IRequest;

public class PrintOrderTicketCommandHandler : IRequestHandler<PrintOrderTicketCommand>
{
    private readonly IEscPosPrinter _escPosPrinter;
    private readonly IApplicationDbContext _context;
    private readonly ITicketGenerator _ticketGenerator;

    public PrintOrderTicketCommandHandler(
        IEscPosPrinter escPosPrinter,
        IApplicationDbContext context,
        ITicketGenerator ticketGenerator)
    {
        _escPosPrinter = escPosPrinter;
        _context = context;
        _ticketGenerator = ticketGenerator;
    }

    public async Task Handle(PrintOrderTicketCommand request, CancellationToken cancellationToken)
    {
        var cashRegister = await _context.CashRegisters.FindAsync(new object[] { request.CashRegisterId }, cancellationToken);
        if (cashRegister == null || !cashRegister.AssignedPrinterId.HasValue) return;

        var printer = await _context.Printers.FindAsync(new object[] { cashRegister.AssignedPrinterId.Value }, cancellationToken);
        if (printer == null) return;

        int widthChars = printer.MaxWidth / 12;
        if (widthChars <= 0) widthChars = 42;
        string ticketText = await _ticketGenerator.GenerateOrderTicketAsync(request.OrderId, cancellationToken, widthCharacters: widthChars);

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
            // Falla de impresión silenciosa
        }
    }
}
