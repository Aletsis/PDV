using MediatR;
using Microsoft.EntityFrameworkCore;
using PDV.Application.Common.Interfaces;
using PDV.Domain.Enums;
using PDV.Domain.Repositories;

namespace PDV.Application.Features.Sales.Commands.PrintSaleTicket;

public record PrintSaleTicketCommand(Guid SaleId, Guid CashRegisterId) : IRequest;

public class PrintSaleTicketCommandHandler : IRequestHandler<PrintSaleTicketCommand>
{
    private readonly ISaleRepository _saleRepository;
    private readonly ITicketGenerator _ticketGenerator;
    private readonly IEscPosPrinter _escPosPrinter;
    private readonly IApplicationDbContext _context;

    public PrintSaleTicketCommandHandler(
        ISaleRepository saleRepository,
        ITicketGenerator ticketGenerator,
        IEscPosPrinter escPosPrinter,
        IApplicationDbContext context)
    {
        _saleRepository = saleRepository;
        _ticketGenerator = ticketGenerator;
        _escPosPrinter = escPosPrinter;
        _context = context;
    }

    public async Task Handle(PrintSaleTicketCommand request, CancellationToken cancellationToken)
    {
        var sale = await _saleRepository.GetByIdAsync(request.SaleId, cancellationToken)
            ?? throw new KeyNotFoundException($"Venta {request.SaleId} no encontrada");

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
        if (widthChars <= 0) widthChars = 48;
        var ticketContent = await _ticketGenerator.GenerateSaleTicketAsync(request.SaleId, cancellationToken, widthCharacters: widthChars);
        var config = await _context.SystemConfigurations.FirstOrDefaultAsync(cancellationToken);
        int copies = config?.TicketCopies > 0 ? config.TicketCopies : 1;
        bool isCashPayment = sale.PaymentMethod == PaymentMethodType.Cash;

        try
        {
            await _escPosPrinter.PrintJobAsync(
                ipAddress: connectionUri,
                port: printer.Port ?? 9100,
                text: ticketContent,
                autoCut: true,
                partialCut: true,
                openDrawerBefore: false,
                openDrawerAfter: isCashPayment, // Apertura automática de cajón en venta en efectivo
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
