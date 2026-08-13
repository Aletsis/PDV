using MediatR;
using Microsoft.EntityFrameworkCore;
using PDV.Application.Common.Interfaces;
using PDV.Domain.Enums;

namespace PDV.Application.Features.Invoices.Commands.PrintInvoiceTicket;

public record PrintInvoiceTicketCommand(Guid InvoiceId, Guid CashRegisterId) : IRequest;

public class PrintInvoiceTicketCommandHandler : IRequestHandler<PrintInvoiceTicketCommand>
{
    private readonly IApplicationDbContext _context;
    private readonly ITicketGenerator _ticketGenerator;
    private readonly IEscPosPrinter _escPosPrinter;

    public PrintInvoiceTicketCommandHandler(
        IApplicationDbContext context,
        ITicketGenerator ticketGenerator,
        IEscPosPrinter escPosPrinter)
    {
        _context = context;
        _ticketGenerator = ticketGenerator;
        _escPosPrinter = escPosPrinter;
    }

    public async Task Handle(PrintInvoiceTicketCommand request, CancellationToken cancellationToken)
    {
        var invoice = await _context.Invoices.FindAsync(new object[] { request.InvoiceId }, cancellationToken)
            ?? throw new KeyNotFoundException($"Factura {request.InvoiceId} no encontrada");

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

        int widthChars = printer.MaxWidth / 12;
        if (widthChars <= 0) widthChars = 42;
        var ticketContent = await _ticketGenerator.GenerateInvoiceTicketAsync(request.InvoiceId, cancellationToken, widthCharacters: widthChars);
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
