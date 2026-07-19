using MediatR;
using PDV.Application.Features.Companies.Dtos;
using PDV.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace PDV.Application.Features.Companies.Queries.GetCompanies;

public record GetCompaniesQuery(bool IncludeInactive = false) : IRequest<List<CompanyDto>>;

public class GetCompaniesQueryHandler : IRequestHandler<GetCompaniesQuery, List<CompanyDto>>
{
    private readonly IApplicationDbContext _context;

    public GetCompaniesQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<CompanyDto>> Handle(GetCompaniesQuery request, CancellationToken cancellationToken)
    {
        var query = _context.Companies.AsNoTracking();

        if (!request.IncludeInactive)
        {
            query = query.Where(c => c.IsActive);
        }

        var list = await query
            .OrderBy(c => c.Name)
            .ToListAsync(cancellationToken);

        return list.Select(c => new CompanyDto(
            c.Id,
            c.Name,
            c.RFC,
            c.FiscalAddress,
            c.Phone,
            c.Email,
            c.IsActive
        )).ToList();
    }
}
