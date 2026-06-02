using MediatR;
using PDV.Application.Common.Interfaces;
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
        // Obtener venta
        var sale = await _saleRepository.GetByIdAsync(request.SaleId, cancellationToken)
            ?? throw new KeyNotFoundException($"Venta {request.SaleId} no encontrada");

        // Obtener caja registradora
        var cashRegister = await _context.CashRegisters.FindAsync(new object[] { request.CashRegisterId }, cancellationToken)
            ?? throw new KeyNotFoundException($"Caja registradora {request.CashRegisterId} no encontrada");

        // Verificar que tenga impresora asignada
        if (!cashRegister.AssignedPrinterId.HasValue)
        {
            // No tiene impresora asignada, salir silenciosamente
            return;
        }

        // Obtener impresora
        var printer = await _context.Printers.FindAsync(new object[] { cashRegister.AssignedPrinterId.Value }, cancellationToken);
        if (printer == null)
        {
            // Impresora no encontrada, salir silenciosamente
            return;
        }

        // Generar contenido del ticket
        var ticketContent = await _ticketGenerator.GenerateSaleTicketAsync(request.SaleId, cancellationToken);

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
            // Si falla la impresión, no fallar la venta
            // Solo registrar el error (podría agregarse logging aquí)
            return;
        }
    }
}
