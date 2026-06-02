using MediatR;
using PDV.Application.Common.Interfaces;

namespace PDV.Application.Features.CashRegisters.Commands.DeleteCashRegister;

public record DeleteCashRegisterCommand(Guid Id) : IRequest<bool>;

public class DeleteCashRegisterCommandHandler : IRequestHandler<DeleteCashRegisterCommand, bool>
{
    private readonly IApplicationDbContext _context;

    public DeleteCashRegisterCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<bool> Handle(DeleteCashRegisterCommand request, CancellationToken cancellationToken)
    {
        var entity = await _context.CashRegisters.FindAsync(new object[] { request.Id }, cancellationToken);
        if (entity == null) return false;

        _context.CashRegisters.Remove(entity);
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }
}
