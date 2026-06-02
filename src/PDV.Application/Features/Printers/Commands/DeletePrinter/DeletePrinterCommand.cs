using MediatR;
using PDV.Application.Common.Interfaces;

namespace PDV.Application.Features.Printers.Commands.DeletePrinter;

public record DeletePrinterCommand(Guid Id) : IRequest<bool>;

public class DeletePrinterCommandHandler : IRequestHandler<DeletePrinterCommand, bool>
{
    private readonly IApplicationDbContext _context;

    public DeletePrinterCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<bool> Handle(DeletePrinterCommand request, CancellationToken cancellationToken)
    {
        var entity = await _context.Printers.FindAsync(new object[] { request.Id }, cancellationToken);
        if (entity == null) return false;

        _context.Printers.Remove(entity);
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }
}
