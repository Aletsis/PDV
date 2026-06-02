using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PDV.Application.Common.Interfaces;
using PDV.Application.Features.Departments.Dtos;

namespace PDV.Application.Features.Departments.Queries.GetDepartments;

public record GetDepartmentsQuery(bool IncludeInactive = true) : IRequest<List<DepartmentDto>>;

public class GetDepartmentsQueryHandler : IRequestHandler<GetDepartmentsQuery, List<DepartmentDto>>
{
    private readonly IApplicationDbContext _context;

    public GetDepartmentsQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<DepartmentDto>> Handle(GetDepartmentsQuery request, CancellationToken cancellationToken)
    {
        var query = _context.Departments.AsQueryable();

        if (!request.IncludeInactive)
        {
            query = query.Where(d => d.IsActive);
        }

        return await query
            .Select(d => new DepartmentDto
            {
                Id = d.Id,
                Name = d.Name,
                Description = d.Description,
                ClasificacionId = d.ClasificacionId,
                IsActive = d.IsActive
            })
            .ToListAsync(cancellationToken);
    }
}
