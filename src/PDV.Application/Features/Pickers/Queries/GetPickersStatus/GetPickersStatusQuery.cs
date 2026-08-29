using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PDV.Application.Common.Interfaces;
using PDV.Application.Features.Pickers.Dtos;
using PDV.Domain.Entities;
using PDV.Domain.Enums;

namespace PDV.Application.Features.Pickers.Queries.GetPickersStatus;

public record GetPickersStatusQuery(Guid BranchId) : IRequest<List<PickerStatusDto>>;

public class GetPickersStatusQueryHandler : IRequestHandler<GetPickersStatusQuery, List<PickerStatusDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly IIdentityService _identityService;

    public GetPickersStatusQueryHandler(
        IApplicationDbContext context,
        IIdentityService identityService)
    {
        _context = context;
        _identityService = identityService;
    }

    public async Task<List<PickerStatusDto>> Handle(GetPickersStatusQuery request, CancellationToken cancellationToken)
    {
        var config = await _context.SystemConfigurations.FirstOrDefaultAsync(cancellationToken);
        int defaultMaxOrders = config?.DefaultMaxPickingOrdersPerPicker > 0 
            ? config.DefaultMaxPickingOrdersPerPicker 
            : 1;

        var branch = await _context.Branches.FirstOrDefaultAsync(b => b.Id == request.BranchId, cancellationToken);
        string branchName = branch?.Name ?? "Sucursal";

        // Obtener todos los usuarios con rol Picker que pertenezcan a la sucursal o no tengan sucursal fija asignada
        var allUsers = await _identityService.GetUsersAsync(cancellationToken);
        var pickerUsers = allUsers
            .Where(u => u.IsActive && 
                        u.Roles.Any(r => r.Equals("Picker", StringComparison.OrdinalIgnoreCase) || 
                                         r.Equals("Surtidor", StringComparison.OrdinalIgnoreCase) ||
                                         r.Equals("Almacen", StringComparison.OrdinalIgnoreCase)) &&
                        (!u.BranchId.HasValue || u.BranchId.Value == request.BranchId))
            .ToList();

        var pickerUserIds = pickerUsers.Select(u => u.Id).ToList();

        // Obtener estados guardados
        var workStatuses = await _context.UserWorkStatuses
            .Where(s => s.BranchId == request.BranchId && pickerUserIds.Contains(s.UserId))
            .ToDictionaryAsync(s => s.UserId, cancellationToken);

        // Obtener conteo de órdenes activas
        var activeOrdersCount = await _context.Orders
            .Where(o => o.BranchId == request.BranchId && 
                        o.Status == OrderStatus.InFulfillment && 
                        o.FilledById != null && 
                        pickerUserIds.Contains(o.FilledById))
            .GroupBy(o => o.FilledById!)
            .Select(g => new { UserId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.UserId, x => x.Count, cancellationToken);

        var result = new List<PickerStatusDto>();

        foreach (var user in pickerUsers)
        {
            workStatuses.TryGetValue(user.Id, out var status);
            activeOrdersCount.TryGetValue(user.Id, out int activeOrders);

            var availabilityStatus = status?.Status ?? PickerAvailabilityStatus.Available;
            int? customCapacity = status?.MaxConcurrentOrders;
            int effectiveCapacity = customCapacity.HasValue && customCapacity.Value > 0 ? customCapacity.Value : defaultMaxOrders;

            result.Add(new PickerStatusDto
            {
                UserId = user.Id,
                UserName = user.UserName,
                FullName = user.FullName,
                EmployeeNumber = user.EmployeeNumber,
                BranchId = request.BranchId,
                BranchName = branchName,
                Status = availabilityStatus,
                CustomMaxCapacity = customCapacity,
                EffectiveMaxCapacity = effectiveCapacity,
                ActiveOrdersCount = activeOrders,
                OrdersCompletedToday = status?.OrdersCompletedToday ?? 0,
                LastStatusChangeAt = status?.LastStatusChangeAt ?? DateTime.UtcNow,
                LastAssignedOrderAt = status?.LastAssignedOrderAt,
                StatusNotes = status?.StatusNotes
            });
        }

        return result.OrderByDescending(p => p.IsEligible)
                     .ThenBy(p => p.FullName)
                     .ToList();
    }
}
