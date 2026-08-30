using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PDV.Application.Common.Helpers;
using PDV.Application.Common.Interfaces;
using PDV.Application.Features.Drivers.Dtos;
using PDV.Domain.Entities;
using PDV.Domain.Enums;

namespace PDV.Application.Features.Drivers.Queries.GetMyDriverStatus;

public record GetMyDriverStatusQuery(string UserId, Guid BranchId) : IRequest<DriverStatusDto?>;

public class GetMyDriverStatusQueryHandler : IRequestHandler<GetMyDriverStatusQuery, DriverStatusDto?>
{
    private readonly IApplicationDbContext _context;
    private readonly IIdentityService _identityService;

    public GetMyDriverStatusQueryHandler(
        IApplicationDbContext context,
        IIdentityService identityService)
    {
        _context = context;
        _identityService = identityService;
    }

    public async Task<DriverStatusDto?> Handle(GetMyDriverStatusQuery request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.UserId) || request.BranchId == Guid.Empty)
            return null;

        var branch = await _context.Branches.FirstOrDefaultAsync(b => b.Id == request.BranchId, cancellationToken);
        string branchName = branch?.Name ?? "Sucursal";

        var user = await _identityService.GetUserByIdAsync(request.UserId, cancellationToken);
        if (user == null || !RoleHelper.HasDeliveryManRole(user.Roles)) return null;

        var workStatus = await _context.UserWorkStatuses
            .FirstOrDefaultAsync(s => s.UserId == request.UserId && s.BranchId == request.BranchId, cancellationToken);

        var activeRoutes = await _context.DeliveryRoutes
            .Include(r => r.Orders)
            .Where(r => r.BranchId == request.BranchId && 
                        r.Status == DeliveryRouteStatus.EnRoute && 
                        r.DeliveryManId == request.UserId)
            .ToListAsync(cancellationToken);

        int activeRoutesCount = activeRoutes.Count;
        int activeOrdersCount = activeRoutes.SelectMany(r => r.Orders).Count(o => o.Status != OrderStatus.Cancelled);

        var availabilityStatus = workStatus?.Status ?? PickerAvailabilityStatus.Available;

        return new DriverStatusDto
        {
            UserId = user.Id,
            UserName = user.UserName,
            FullName = user.FullName,
            EmployeeNumber = user.EmployeeNumber,
            BranchId = request.BranchId,
            BranchName = branchName,
            Status = availabilityStatus,
            ActiveRoutesCount = activeRoutesCount,
            ActiveOrdersCount = activeOrdersCount,
            DeliveriesCompletedToday = workStatus?.OrdersCompletedToday ?? 0,
            LastStatusChangeAt = workStatus?.LastStatusChangeAt ?? DateTime.Now,
            LastAssignedRouteAt = workStatus?.LastAssignedOrderAt,
            StatusNotes = workStatus?.StatusNotes
        };
    }
}
