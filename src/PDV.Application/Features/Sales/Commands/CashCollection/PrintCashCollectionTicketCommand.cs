using MediatR;
using PDV.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace PDV.Application.Features.Sales.Commands.CashCollection;

public record PrintCashCollectionTicketCommand(Guid CollectionId, Guid CashRegisterId) : IRequest;

public class PrintCashCollectionTicketCommandHandler : IRequestHandler<PrintCashCollectionTicketCommand>
{
    private readonly ITicketGenerator _ticketGenerator;
    private readonly IEscPosPrinter _escPosPrinter;
    private readonly IApplicationDbContext _context;

    public PrintCashCollectionTicketCommandHandler(
        ITicketGenerator ticketGenerator,
        IEscPosPrinter escPosPrinter,
        IApplicationDbContext context)
    {
        _ticketGenerator = ticketGenerator;
        _escPosPrinter = escPosPrinter;
        _context = context;
    }

    public async Task Handle(PrintCashCollectionTicketCommand request, CancellationToken cancellationToken)
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

        // Generar contenido del ticket
        var ticketContent = await _ticketGenerator.GenerateCashCollectionTicketAsync(request.CollectionId, cancellationToken);

        // Imprimir
        try
        {
            await _escPosPrinter.PrintTextAsync(
                printer.IpAddress ?? string.Empty,
                printer.Port ?? 9100,
                ticketContent,
                encodingCodePage: 28591, // Latin-1 para español
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
