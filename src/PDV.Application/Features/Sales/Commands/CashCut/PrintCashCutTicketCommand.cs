using MediatR;
using Microsoft.EntityFrameworkCore;
using PDV.Application.Common.Interfaces;
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
        var cut = await _context.CashCuts.FindAsync(new object[] { request.CutId }, cancellationToken)
            ?? throw new KeyNotFoundException($"Corte de caja {request.CutId} no encontrado");

        var cashRegister = await _context.CashRegisters.FindAsync(new object[] { request.CashRegisterId }, cancellationToken);
        if (cashRegister == null || !cashRegister.AssignedPrinterId.HasValue)
        {
            return;
        }

        var printer = await _context.Printers.FindAsync(new object[] { cashRegister.AssignedPrinterId.Value }, cancellationToken);
        if (printer == null)
        {
            return;
        }

        string connectionUri = printer.ConnectionType switch
        {
            PrinterConnectionType.Network => printer.IpAddress ?? string.Empty,
            PrinterConnectionType.Usb => $"usb://{printer.DevicePath}",
            PrinterConnectionType.Serial => $"serial://{printer.DevicePath}?baud={printer.CodePage}",
            _ => printer.IpAddress ?? string.Empty
        };

        var ticketContent = await _ticketGenerator.GenerateCashCutTicketAsync(request.CutId, cancellationToken);
        var config = await _context.SystemConfigurations.FirstOrDefaultAsync(cancellationToken);
        int copies = config?.TicketCopies > 0 ? config.TicketCopies : 1;

        try
        {
            await _escPosPrinter.PrintJobAsync(
                ipAddress: connectionUri,
                port: printer.Port ?? 9100,
                text: ticketContent,
                autoCut: true,
                partialCut: true,
                openDrawerBefore: false,
                openDrawerAfter: false,
                copies: copies,
                encodingCodePage: printer.CodePage > 0 ? printer.CodePage : 1252,
                cancellationToken: cancellationToken
            );
        }
        catch (Exception)
        {
            return;
        }
    }
}
