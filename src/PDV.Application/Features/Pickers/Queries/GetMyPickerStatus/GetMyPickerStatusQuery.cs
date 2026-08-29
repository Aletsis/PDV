using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PDV.Application.Common.Helpers;
using PDV.Application.Common.Interfaces;
using PDV.Application.Features.Pickers.Dtos;
using PDV.Domain.Entities;
using PDV.Domain.Enums;

namespace PDV.Application.Features.Pickers.Queries.GetMyPickerStatus;

public record GetMyPickerStatusQuery(string UserId, Guid BranchId) : IRequest<PickerStatusDto?>;

public class GetMyPickerStatusQueryHandler : IRequestHandler<GetMyPickerStatusQuery, PickerStatusDto?>
{
    private readonly IApplicationDbContext _context;
    private readonly IIdentityService _identityService;

    public GetMyPickerStatusQueryHandler(
        IApplicationDbContext context,
        IIdentityService identityService)
    {
        _context = context;
        _identityService = identityService;
    }

    public async Task<PickerStatusDto?> Handle(GetMyPickerStatusQuery request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.UserId) || request.BranchId == Guid.Empty)
            return null;

        var config = await _context.SystemConfigurations.FirstOrDefaultAsync(cancellationToken);
        int defaultMaxOrders = config?.DefaultMaxPickingOrdersPerPicker > 0 
            ? config.DefaultMaxPickingOrdersPerPicker 
            : 1;

        var branch = await _context.Branches.FirstOrDefaultAsync(b => b.Id == request.BranchId, cancellationToken);
        string branchName = branch?.Name ?? "Sucursal";

        var user = await _identityService.GetUserByIdAsync(request.UserId, cancellationToken);
        if (user == null || !RoleHelper.HasPickerRole(user.Roles)) return null;

        var workStatus = await _context.UserWorkStatuses
            .FirstOrDefaultAsync(s => s.UserId == request.UserId && s.BranchId == request.BranchId, cancellationToken);

        int activeOrders = await _context.Orders
            .CountAsync(o => o.BranchId == request.BranchId && 
                             o.Status == OrderStatus.InFulfillment && 
                             o.FilledById == request.UserId, cancellationToken);

        var availabilityStatus = workStatus?.Status ?? PickerAvailabilityStatus.Available;
        int? customCapacity = workStatus?.MaxConcurrentOrders;
        int effectiveCapacity = customCapacity.HasValue && customCapacity.Value > 0 ? customCapacity.Value : defaultMaxOrders;

        return new PickerStatusDto
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
            OrdersCompletedToday = workStatus?.OrdersCompletedToday ?? 0,
            LastStatusChangeAt = workStatus?.LastStatusChangeAt ?? DateTime.UtcNow,
            LastAssignedOrderAt = workStatus?.LastAssignedOrderAt,
            StatusNotes = workStatus?.StatusNotes
        };
    }
}
