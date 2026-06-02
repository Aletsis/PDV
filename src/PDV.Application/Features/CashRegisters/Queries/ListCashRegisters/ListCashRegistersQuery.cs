using MediatR;
using Microsoft.EntityFrameworkCore;
using PDV.Application.Common.Interfaces;
using PDV.Application.Features.CashRegisters.Dtos;

namespace PDV.Application.Features.CashRegisters.Queries.ListCashRegisters;

public record ListCashRegistersQuery : IRequest<List<CashRegisterDto>>;

public class ListCashRegistersQueryHandler : IRequestHandler<ListCashRegistersQuery, List<CashRegisterDto>>
{
    private readonly IApplicationDbContext _context;

    public ListCashRegistersQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<CashRegisterDto>> Handle(ListCashRegistersQuery request, CancellationToken cancellationToken)
    {
        return await _context.CashRegisters
            .AsNoTracking()
            .Include(e => e.Branch)
            .Include(e => e.AssignedEmployee)
            .Include(e => e.AssignedPrinter)
            .OrderBy(e => e.Name)
            .Select(e => new CashRegisterDto
            {
                Id = e.Id,
                Name = e.Name,
                Location = e.Location,
                IsActive = e.IsActive,
                IpAddress = e.IpAddress,
                Mode = e.Mode,
                InitialCash = 0,
                BranchId = e.BranchId,
                BranchName = e.Branch != null ? e.Branch.Name : string.Empty,
                AssignedEmployeeId = e.AssignedEmployeeId,
                AssignedEmployeeName = e.AssignedEmployee != null ? e.AssignedEmployee.Name : null,
                AssignedPrinterId = e.AssignedPrinterId,
                AssignedPrinterName = e.AssignedPrinter != null ? e.AssignedPrinter.Name : null,
            })
            .ToListAsync(cancellationToken);
    }
}

