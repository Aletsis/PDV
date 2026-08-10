using MediatR;
using PDV.Application.Common.Interfaces;
using PDV.Domain.Enums;

namespace PDV.Application.Features.CashRegisters.Commands.OpenCashDrawer;

public record OpenCashDrawerCommand(Guid CashRegisterId) : IRequest<bool>;

public class OpenCashDrawerCommandHandler : IRequestHandler<OpenCashDrawerCommand, bool>
{
    private readonly IApplicationDbContext _context;
    private readonly IEscPosPrinter _escPosPrinter;

    public OpenCashDrawerCommandHandler(
        IApplicationDbContext context,
        IEscPosPrinter escPosPrinter)
    {
        _context = context;
        _escPosPrinter = escPosPrinter;
    }

    public async Task<bool> Handle(OpenCashDrawerCommand request, CancellationToken cancellationToken)
    {
        if (request.CashRegisterId == Guid.Empty) return false;

        var cashRegister = await _context.CashRegisters.FindAsync(new object[] { request.CashRegisterId }, cancellationToken);
        if (cashRegister == null || !cashRegister.AssignedPrinterId.HasValue)
        {
            return false;
        }

        var printer = await _context.Printers.FindAsync(new object[] { cashRegister.AssignedPrinterId.Value }, cancellationToken);
        if (printer == null)
        {
            return false;
        }

        string connectionUri = printer.ConnectionType switch
        {
            PrinterConnectionType.Network => printer.IpAddress ?? string.Empty,
            PrinterConnectionType.Usb => $"usb://{printer.DevicePath}",
            PrinterConnectionType.Serial => $"serial://{printer.DevicePath}?baud={printer.CodePage}",
            _ => printer.IpAddress ?? string.Empty
        };

        try
        {
            await _escPosPrinter.OpenDrawerAsync(connectionUri, printer.Port ?? 9100, cancellationToken);
            return true;
        }
        catch (Exception)
        {
            // Silencioso para no romper flujos si el hardware no responde
            return false;
        }
    }
}
