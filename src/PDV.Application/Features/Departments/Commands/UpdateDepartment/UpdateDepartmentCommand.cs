using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using PDV.Application.Common.Interfaces;

namespace PDV.Application.Features.Departments.Commands.UpdateDepartment;

public record UpdateDepartmentCommand(
    Guid Id,
    string Name,
    string? Description,
    int? ClasificacionId,
    bool IsActive
) : IRequest;

public class UpdateDepartmentCommandHandler : IRequestHandler<UpdateDepartmentCommand>
{
    private readonly IApplicationDbContext _context;

    public UpdateDepartmentCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task Handle(UpdateDepartmentCommand request, CancellationToken cancellationToken)
    {
        var entity = await _context.Departments.FindAsync(new object[] { request.Id }, cancellationToken);

        if (entity == null)
        {
            throw new KeyNotFoundException("Department not found.");
        }

        entity.Update(request.Name, request.Description, request.ClasificacionId);

        if (request.IsActive && !entity.IsActive)
        {
            entity.Activate();
        }
        else if (!request.IsActive && entity.IsActive)
        {
            entity.Deactivate();
        }

        await _context.SaveChangesAsync(cancellationToken);
    }
}
