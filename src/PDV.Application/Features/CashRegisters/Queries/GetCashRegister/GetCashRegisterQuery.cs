using MediatR;
using Microsoft.EntityFrameworkCore;
using PDV.Application.Common.Interfaces;
using PDV.Application.Features.CashRegisters.Dtos;

namespace PDV.Application.Features.CashRegisters.Queries.GetCashRegister;

public record GetCashRegisterQuery(Guid Id) : IRequest<CashRegisterDto?>;

public class GetCashRegisterQueryHandler : IRequestHandler<GetCashRegisterQuery, CashRegisterDto?>
{
    private readonly IApplicationDbContext _context;
    private readonly IIdentityService _identityService;

    public GetCashRegisterQueryHandler(IApplicationDbContext context, IIdentityService identityService)
    {
        _context = context;
        _identityService = identityService;
    }

    public async Task<CashRegisterDto?> Handle(GetCashRegisterQuery request, CancellationToken cancellationToken)
    {
        var e = await _context.CashRegisters
            .AsNoTracking()
            .Include(x => x.Branch)
            .Include(x => x.AssignedPrinter)
            .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);

        if (e == null) return null;

        var dto = new CashRegisterDto
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
            AssignedUserId = e.AssignedUserId,
            AssignedPrinterId = e.AssignedPrinterId,
            AssignedPrinterName = e.AssignedPrinter?.Name,
        };

        if (!string.IsNullOrEmpty(dto.AssignedUserId))
        {
            try
            {
                var user = await _identityService.GetUserByIdAsync(dto.AssignedUserId, cancellationToken);
                if (user != null)
                {
                    dto.AssignedUserName = user.FullName;
                }
            }
            catch
            {
                // En ambientes offline, omitimos el nombre
            }
        }

        return dto;
    }
}
