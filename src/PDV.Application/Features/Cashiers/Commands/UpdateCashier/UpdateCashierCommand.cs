using MediatR;
using PDV.Application.Common.Interfaces;
using PDV.Domain.Entities;
using PDV.Domain.Enums;

namespace PDV.Application.Features.Cashiers.Commands.UpdateCashier;

public record UpdateCashierCommand(Guid Id, string Name, string EmployeeId, string? UserId, bool IsActive) : IRequest<bool>;

public class UpdateCashierCommandHandler : IRequestHandler<UpdateCashierCommand, bool>
{
    private readonly IApplicationDbContext _context;

    public UpdateCashierCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<bool> Handle(UpdateCashierCommand request, CancellationToken cancellationToken)
    {
        var entity = await _context.Employees.FindAsync(new object[] { request.Id }, cancellationToken);
        if (entity == null) return false;

        entity.Update(request.Name, request.EmployeeId, EmployeeRole.Cashier);
        
        if (!string.IsNullOrWhiteSpace(request.UserId))
        {
            entity.LinkUserId(request.UserId);
        }

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
