using System;
using System.Collections.Generic;
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

namespace PDV.Application.Features.Drivers.Queries.GetDriversStatus;

public record GetDriversStatusQuery(Guid BranchId) : IRequest<List<DriverStatusDto>>;

public class GetDriversStatusQueryHandler : IRequestHandler<GetDriversStatusQuery, List<DriverStatusDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly IIdentityService _identityService;

    public GetDriversStatusQueryHandler(
        IApplicationDbContext context,
        IIdentityService identityService)
    {
        _context = context;
        _identityService = identityService;
    }

    public async Task<List<DriverStatusDto>> Handle(GetDriversStatusQuery request, CancellationToken cancellationToken)
    {
        var branch = await _context.Branches.FirstOrDefaultAsync(b => b.Id == request.BranchId, cancellationToken);
        string branchName = branch?.Name ?? "Sucursal";

        // Obtener todos los usuarios con rol DeliveryMan que pertenezcan a la sucursal o no tengan sucursal fija asignada
        var allUsers = await _identityService.GetUsersAsync(cancellationToken);
        var driverUsers = allUsers
            .Where(u => u.IsActive && 
                        RoleHelper.HasDeliveryManRole(u.Roles) &&
                        (!u.BranchId.HasValue || u.BranchId.Value == request.BranchId))
            .ToList();

        var driverUserIds = driverUsers.Select(u => u.Id).ToList();

        // Obtener estados guardados en UserWorkStatuses
        var workStatuses = await _context.UserWorkStatuses
            .Where(s => s.BranchId == request.BranchId && driverUserIds.Contains(s.UserId))
            .ToDictionaryAsync(s => s.UserId, cancellationToken);

        // Obtener rutas activas (En tránsito)
        var activeRoutes = await _context.DeliveryRoutes
            .Include(r => r.Orders)
            .Where(r => r.BranchId == request.BranchId && 
                        r.Status == DeliveryRouteStatus.EnRoute && 
                        r.DeliveryManId != null && 
                        driverUserIds.Contains(r.DeliveryManId))
            .ToListAsync(cancellationToken);

        var activeRoutesByDriver = activeRoutes
            .GroupBy(r => r.DeliveryManId!)
            .ToDictionary(
                g => g.Key, 
                g => new 
                {
                    RouteCount = g.Count(),
                    OrderCount = g.SelectMany(r => r.Orders).Count(o => o.Status != OrderStatus.Cancelled)
                }
            );

        var result = new List<DriverStatusDto>();

        foreach (var user in driverUsers)
        {
            workStatuses.TryGetValue(user.Id, out var status);
            activeRoutesByDriver.TryGetValue(user.Id, out var routeStats);

            var availabilityStatus = status?.Status ?? PickerAvailabilityStatus.Available;
            int activeRoutesCount = routeStats?.RouteCount ?? 0;
            int activeOrdersCount = routeStats?.OrderCount ?? 0;

            result.Add(new DriverStatusDto
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
                DeliveriesCompletedToday = status?.OrdersCompletedToday ?? 0,
                LastStatusChangeAt = status?.LastStatusChangeAt ?? DateTime.Now,
                LastAssignedRouteAt = status?.LastAssignedOrderAt,
                StatusNotes = status?.StatusNotes
            });
        }

        return result.OrderByDescending(d => d.IsEligible)
                     .ThenBy(d => d.FullName)
                     .ToList();
    }
}
