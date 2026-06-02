using MediatR;
using Microsoft.EntityFrameworkCore;
using PDV.Application.Common.Interfaces;
using PDV.Application.Features.CashRegisters.Dtos;

namespace PDV.Application.Features.CashRegisters.Queries.GetCashRegister;

public record GetCashRegisterQuery(Guid Id) : IRequest<CashRegisterDto?>;

public class GetCashRegisterQueryHandler : IRequestHandler<GetCashRegisterQuery, CashRegisterDto?>
{
    private readonly IApplicationDbContext _context;

    public GetCashRegisterQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<CashRegisterDto?> Handle(GetCashRegisterQuery request, CancellationToken cancellationToken)
    {
        var e = await _context.CashRegisters
            .AsNoTracking()
            .Include(x => x.Branch)
            .Include(x => x.AssignedEmployee)
            .Include(x => x.AssignedPrinter)
            .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);

        if (e == null) return null;

        return new CashRegisterDto
        {
            Id = e.Id,
            Name = e.Name,
            Location = e.Location,
            IsActive = e.IsActive,
            IpAddress = e.IpAddress,
            Mode = e.Mode,
            InitialCash = 0,
            BranchId = e.BranchId,
            BranchName = e.Branch?.Name ?? string.Empty,
            AssignedEmployeeId = e.AssignedEmployeeId,
            AssignedEmployeeName = e.AssignedEmployee?.Name,
            AssignedPrinterId = e.AssignedPrinterId,
            AssignedPrinterName = e.AssignedPrinter?.Name,
        };
    }
}

