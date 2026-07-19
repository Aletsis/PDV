using MediatR;
using PDV.Application.Common.Interfaces;
using PDV.Domain.Common.Models;
using Microsoft.EntityFrameworkCore;

namespace PDV.Application.Features.Companies.Commands.DeleteCompany;

public record DeleteCompanyCommand(Guid Id) : IRequest<Result>;

public class DeleteCompanyCommandHandler : IRequestHandler<DeleteCompanyCommand, Result>
{
    private readonly IApplicationDbContext _context;

    public DeleteCompanyCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result> Handle(DeleteCompanyCommand request, CancellationToken cancellationToken)
    {
        var company = await _context.Companies.FirstOrDefaultAsync(c => c.Id == request.Id, cancellationToken);
        if (company == null)
        {
            return Result.Failure(Error.NotFound("Company.NotFound", $"No se encontró la empresa con ID '{request.Id}'."));
        }

        try
        {
            _context.Companies.Remove(company);
            await _context.SaveChangesAsync(cancellationToken);
            
            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Failure(Error.Failure("Company.DeleteFailed", ex.Message));
        }
    }
}
