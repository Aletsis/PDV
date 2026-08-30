using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PDV.Application.Common.Helpers;
using PDV.Application.Common.Interfaces;
using PDV.Domain.Entities;
using PDV.Domain.Enums;
using PDV.Domain.Exceptions;

namespace PDV.Application.Features.Drivers.Commands.SetDriverStatus;

public record SetDriverStatusCommand : IRequest<bool>
{
    public string UserId { get; set; } = string.Empty;
    public Guid BranchId { get; set; }
    public PickerAvailabilityStatus Status { get; set; }
    public string? Notes { get; set; }
}

public class SetDriverStatusCommandHandler : IRequestHandler<SetDriverStatusCommand, bool>
{
    private readonly IApplicationDbContext _context;
    private readonly IIdentityService? _identityService;
    private readonly IRealTimeSyncNotifier? _syncNotifier;

    public SetDriverStatusCommandHandler(
        IApplicationDbContext context,
        IIdentityService? identityService = null,
        IRealTimeSyncNotifier? syncNotifier = null)
    {
        _context = context;
        _identityService = identityService;
        _syncNotifier = syncNotifier;
    }

    public async Task<bool> Handle(SetDriverStatusCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.UserId))
            throw new DomainException("El ID de usuario es requerido.");

        if (request.BranchId == Guid.Empty)
            throw new DomainException("La sucursal es requerida.");

        if (_identityService != null)
        {
            var user = await _identityService.GetUserByIdAsync(request.UserId, cancellationToken);
            if (user == null || !RoleHelper.HasDeliveryManRole(user.Roles))
            {
                throw new DomainException("El usuario seleccionado no cuenta con el rol de Repartidor (DeliveryMan).");
            }
        }

        var userStatus = await _context.UserWorkStatuses
            .FirstOrDefaultAsync(s => s.UserId == request.UserId, cancellationToken);

        if (userStatus == null)
        {
            userStatus = new UserWorkStatus(request.UserId, request.BranchId);
            _context.UserWorkStatuses.Add(userStatus);
        }
        else if (userStatus.BranchId != request.BranchId)
        {
            userStatus.ChangeBranch(request.BranchId);
        }

        switch (request.Status)
        {
            case PickerAvailabilityStatus.Available:
                userStatus.SetAvailable();
                break;
            case PickerAvailabilityStatus.MealBreak:
                userStatus.SetMealBreak(request.Notes);
                break;
            case PickerAvailabilityStatus.OperationalBreak:
                userStatus.SetOperationalBreak(request.Notes);
                break;
            case PickerAvailabilityStatus.OffDuty:
                userStatus.SetOffDuty(request.Notes);
                break;
            case PickerAvailabilityStatus.Busy:
                userStatus.SetOperationalBreak("Ocupado en tareas especiales de entrega");
                break;
        }

        await _context.SaveChangesAsync(cancellationToken);

        if (_syncNotifier != null)
        {
            await _syncNotifier.NotifyEntityChangedAsync("DriverStatus", cancellationToken);
            await _syncNotifier.NotifyEntityChangedAsync("UserWorkStatus", cancellationToken);
        }

        return true;
    }
}
