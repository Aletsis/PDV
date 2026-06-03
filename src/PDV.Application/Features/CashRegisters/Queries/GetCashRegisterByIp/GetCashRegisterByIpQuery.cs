using MediatR;
using Microsoft.EntityFrameworkCore;
using PDV.Application.Common.Interfaces;
using PDV.Application.Features.CashRegisters.Dtos;
using PDV.Application.Common.Helpers;

namespace PDV.Application.Features.CashRegisters.Queries.GetCashRegisterByIp;

/// <summary>
/// Busca la caja registradora vinculada a una dirección IP específica.
/// Retorna null si no existe ninguna caja con esa IP.
/// Usado por el middleware de identificación de terminal en el cliente POS.
/// </summary>
public record GetCashRegisterByIpQuery(string IpAddress) : IRequest<CashRegisterDto?>;
public class GetCashRegisterByIpQueryHandler : IRequestHandler<GetCashRegisterByIpQuery, CashRegisterDto?>
{
    private readonly IApplicationDbContext _context;
    private readonly IIdentityService _identityService;

    public GetCashRegisterByIpQueryHandler(IApplicationDbContext context, IIdentityService identityService)
    {
        _context = context;
        _identityService = identityService;
    }

    public async Task<CashRegisterDto?> Handle(GetCashRegisterByIpQuery request, CancellationToken cancellationToken)
    {
        var normalized = request.IpAddress?.Trim();
        if (string.IsNullOrWhiteSpace(normalized)) return null;

        var isLoopback = IpAddressHelper.IsLoopback(normalized);
        var localIps = IpAddressHelper.GetLocalIpAddresses();

        var e = await _context.CashRegisters
            .AsNoTracking()
            .Include(x => x.Branch)
            .Include(x => x.AssignedPrinter)
            .FirstOrDefaultAsync(x => x.IpAddress == normalized && x.IsActive, cancellationToken);

        if (e == null && (isLoopback || localIps.Contains(normalized)))
        {
            var candidateIps = new List<string>(localIps) { "::1", "127.0.0.1", "0.0.0.1" };
            e = await _context.CashRegisters
                .AsNoTracking()
                .Include(x => x.Branch)
                .Include(x => x.AssignedPrinter)
                .FirstOrDefaultAsync(x => x.IpAddress != null && candidateIps.Contains(x.IpAddress) && x.IsActive, cancellationToken);
        }

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
