using MediatR;
using PDV.Application.Common.Interfaces;
using PDV.Domain.Entities;

namespace PDV.Application.Features.Printers.Commands.CreatePrinter;

public record CreatePrinterCommand(
    string Name, 
    string IpAddress, 
    int Port, 
    int? CodePage, 
    int MaxWidth, 
    bool IsActive,
    PDV.Domain.Enums.PrinterConnectionType ConnectionType = PDV.Domain.Enums.PrinterConnectionType.Network
) : IRequest<Guid>;

public class CreatePrinterCommandHandler : IRequestHandler<CreatePrinterCommand, Guid>
{
    private readonly IApplicationDbContext _context;

    public CreatePrinterCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Guid> Handle(CreatePrinterCommand request, CancellationToken cancellationToken)
    {
        var entity = new Printer(
            request.Name,
            request.ConnectionType,
            request.CodePage ?? 1252,
            request.MaxWidth,
            request.IpAddress,
            request.Port
        );

        if (!request.IsActive)
        {
            entity.Deactivate();
        }

        _context.Printers.Add(entity);
        await _context.SaveChangesAsync(cancellationToken);
        return entity.Id;
    }
}
