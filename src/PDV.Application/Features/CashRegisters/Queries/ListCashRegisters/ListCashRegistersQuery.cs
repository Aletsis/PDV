using MediatR;
using Microsoft.EntityFrameworkCore;
using PDV.Application.Common.Interfaces;
using PDV.Application.Features.CashRegisters.Dtos;

namespace PDV.Application.Features.CashRegisters.Queries.ListCashRegisters;

public record ListCashRegistersQuery : IRequest<List<CashRegisterDto>>;

public class ListCashRegistersQueryHandler : IRequestHandler<ListCashRegistersQuery, List<CashRegisterDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly IIdentityService _identityService;

    public ListCashRegistersQueryHandler(IApplicationDbContext context, IIdentityService identityService)
    {
        _context = context;
        _identityService = identityService;
    }

    public async Task<List<CashRegisterDto>> Handle(ListCashRegistersQuery request, CancellationToken cancellationToken)
    {
        var registers = await _context.CashRegisters
            .AsNoTracking()
            .Include(e => e.Branch)
            .Include(e => e.AssignedPrinter)
            .OrderBy(e => e.Name)
            .ToListAsync(cancellationToken);

        var dtos = registers.Select(e => new CashRegisterDto
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
            AssignedUserId = e.AssignedUserId,
            AssignedPrinterId = e.AssignedPrinterId,
            AssignedPrinterName = e.AssignedPrinter != null ? e.AssignedPrinter.Name : null,
        }).ToList();

        var userIds = dtos.Where(d => !string.IsNullOrEmpty(d.AssignedUserId)).Select(d => d.AssignedUserId!).Distinct().ToList();
        if (userIds.Any())
        {
            try
            {
                var users = await _identityService.GetUsersAsync(cancellationToken);
                var usersDict = users.ToDictionary(u => u.Id, u => u.FullName);
                foreach (var dto in dtos)
                {
                    if (dto.AssignedUserId != null && usersDict.TryGetValue(dto.AssignedUserId, out var name))
                    {
                        dto.AssignedUserName = name;
                    }
                }
            }
            catch
            {
                // En ambientes offline o sin conexión al servicio de identidad, omitimos los nombres
            }
        }

        return dtos;
    }
}

