using MediatR;
using Microsoft.EntityFrameworkCore;
using PDV.Application.Common.Interfaces;
using PDV.Application.Features.Cashiers.Dtos;

namespace PDV.Application.Features.Cashiers.Queries.GetCashier;

public record GetCashierQuery(Guid Id) : IRequest<CashierDto?>;

public class GetCashierQueryHandler : IRequestHandler<GetCashierQuery, CashierDto?>
{
    private readonly IApplicationDbContext _context;

    public GetCashierQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<CashierDto?> Handle(GetCashierQuery request, CancellationToken cancellationToken)
    {
        var e = await _context.Employees.AsNoTracking().FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);
        if (e == null) return null;
        return new CashierDto { Id = e.Id, Name = e.Name, EmployeeId = e.EmployeeCode, IsActive = e.IsActive, UserId = e.UserId };
    }
}
