using MediatR;
using PDV.Application.Common.Interfaces;

namespace PDV.Application.Features.Clients.Commands.DeleteClient;

public record DeleteClientCommand(Guid Id) : IRequest<bool>;

public class DeleteClientCommandHandler : IRequestHandler<DeleteClientCommand, bool>
{
    private readonly IApplicationDbContext _context;

    public DeleteClientCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<bool> Handle(DeleteClientCommand request, CancellationToken cancellationToken)
    {
        var entity = await _context.Clients.FindAsync(new object[] { request.Id }, cancellationToken);

        if (entity == null)
            return false;

        // Soft delete
        entity.Deactivate();

        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }
}
