using MediatR;
using Microsoft.EntityFrameworkCore;
using PDV.Application.Common.Interfaces;
using PDV.Application.Features.Cashiers.Dtos;

namespace PDV.Application.Features.Cashiers.Queries.ListCashiers;

public record ListCashiersQuery : IRequest<List<CashierDto>>;

public class ListCashiersQueryHandler : IRequestHandler<ListCashiersQuery, List<CashierDto>>
{
    private readonly IApplicationDbContext _context;

    public ListCashiersQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<CashierDto>> Handle(ListCashiersQuery request, CancellationToken cancellationToken)
    {
        return await _context.Employees
            .AsNoTracking()
            .Select(e => new CashierDto { Id = e.Id, Name = e.Name, EmployeeId = e.EmployeeCode, IsActive = e.IsActive, UserId = e.UserId })
            .ToListAsync(cancellationToken);
    }
}
