using MediatR;
using PDV.Application.Common.Interfaces;

namespace PDV.Application.Features.Cashiers.Commands.DeleteCashier;

public record DeleteCashierCommand(Guid Id) : IRequest<bool>;

public class DeleteCashierCommandHandler : IRequestHandler<DeleteCashierCommand, bool>
{
    private readonly IApplicationDbContext _context;

    public DeleteCashierCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<bool> Handle(DeleteCashierCommand request, CancellationToken cancellationToken)
    {
        var entity = await _context.Employees.FindAsync(new object[] { request.Id }, cancellationToken);
        if (entity == null) return false;

        _context.Employees.Remove(entity);
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }
}
