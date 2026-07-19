using MediatR;
using PDV.Application.Features.Companies.Dtos;
using PDV.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;
using PDV.Domain.Common.Models;

namespace PDV.Application.Features.Companies.Queries.GetCompanyById;

public record GetCompanyByIdQuery(Guid Id) : IRequest<Result<CompanyDto>>;

public class GetCompanyByIdQueryHandler : IRequestHandler<GetCompanyByIdQuery, Result<CompanyDto>>
{
    private readonly IApplicationDbContext _context;

    public GetCompanyByIdQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<CompanyDto>> Handle(GetCompanyByIdQuery request, CancellationToken cancellationToken)
    {
        var company = await _context.Companies
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == request.Id, cancellationToken);

        if (company == null)
        {
            return Result.Failure<CompanyDto>(Error.NotFound("Company.NotFound", $"No se encontró la empresa con ID '{request.Id}'."));
        }

        var dto = new CompanyDto(
            company.Id,
            company.Name,
            company.RFC,
            company.FiscalAddress,
            company.Phone,
            company.Email,
            company.IsActive
        );

        return Result.Success(dto);
    }
}
