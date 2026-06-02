using MediatR;
using PDV.Application.Common.Interfaces;
using PDV.Domain.Entities;

namespace PDV.Application.Features.Printers.Commands.UpdatePrinter;

public record UpdatePrinterCommand(Guid Id, string Name, string IpAddress, int Port, int CodePage, int MaxWidth, bool IsActive) : IRequest<bool>;

public class UpdatePrinterCommandHandler : IRequestHandler<UpdatePrinterCommand, bool>
{
    private readonly IApplicationDbContext _context;

    public UpdatePrinterCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<bool> Handle(UpdatePrinterCommand request, CancellationToken cancellationToken)
    {
        var entity = await _context.Printers.FindAsync(new object[] { request.Id }, cancellationToken);
        if (entity == null) return false;

        entity.Update(request.Name, request.CodePage, request.MaxWidth, request.IpAddress, request.Port);
        
        if (request.IsActive && !entity.IsActive)
        {
            entity.Activate();
        }
        else if (!request.IsActive && entity.IsActive)
        {
            entity.Deactivate();
        }

        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }
}
