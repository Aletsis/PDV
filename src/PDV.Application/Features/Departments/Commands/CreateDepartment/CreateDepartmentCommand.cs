using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using PDV.Application.Common.Interfaces;
using PDV.Domain.Entities;

namespace PDV.Application.Features.Departments.Commands.CreateDepartment;

public record CreateDepartmentCommand(
    string Name,
    string? Description,
    int? ClasificacionId,
    bool IsActive
) : IRequest<Guid>;

public class CreateDepartmentCommandHandler : IRequestHandler<CreateDepartmentCommand, Guid>
{
    private readonly IApplicationDbContext _context;

    public CreateDepartmentCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Guid> Handle(CreateDepartmentCommand request, CancellationToken cancellationToken)
    {
        var entity = new Department(
            request.Name,
            request.Description,
            request.ClasificacionId
        );

        if (!request.IsActive)
        {
            entity.Deactivate();
        }

        _context.Departments.Add(entity);
        await _context.SaveChangesAsync(cancellationToken);

        return entity.Id;
    }
}
