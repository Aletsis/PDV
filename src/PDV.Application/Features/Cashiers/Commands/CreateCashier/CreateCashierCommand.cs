using MediatR;
using PDV.Application.Common.Interfaces;
using PDV.Application.Features.Cashiers.Dtos;
using PDV.Domain.Entities;

namespace PDV.Application.Features.Cashiers.Commands.CreateCashier;

public record CreateCashierCommand(string Name, string EmployeeId, string? UserId, bool IsActive = true) : IRequest<Guid>;

public class CreateCashierCommandHandler : IRequestHandler<CreateCashierCommand, Guid>
{
    private readonly IApplicationDbContext _context;

    public CreateCashierCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Guid> Handle(CreateCashierCommand request, CancellationToken cancellationToken)
    {
        var entity = new Employee(
            request.Name,
            request.EmployeeId, // maps to EmployeeCode
            PDV.Domain.Enums.EmployeeRole.Cashier,
            request.UserId
        );

        if (!request.IsActive)
        {
            entity.Deactivate();
        }

        _context.Employees.Add(entity);
        await _context.SaveChangesAsync(cancellationToken);

        return entity.Id;
    }
}
