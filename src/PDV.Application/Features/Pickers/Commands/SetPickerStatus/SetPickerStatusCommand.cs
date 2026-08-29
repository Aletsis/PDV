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

namespace PDV.Application.Features.Pickers.Commands.SetPickerStatus;

public record SetPickerStatusCommand : IRequest<bool>
{
    public string UserId { get; set; } = string.Empty;
    public Guid BranchId { get; set; }
    public PickerAvailabilityStatus Status { get; set; }
    public string? Notes { get; set; }
}

public class SetPickerStatusCommandHandler : IRequestHandler<SetPickerStatusCommand, bool>
{
    private readonly IApplicationDbContext _context;
    private readonly IPickerDispatcherService _pickerDispatcher;
    private readonly IIdentityService? _identityService;
    private readonly IRealTimeSyncNotifier? _syncNotifier;

    public SetPickerStatusCommandHandler(
        IApplicationDbContext context,
        IPickerDispatcherService pickerDispatcher,
        IIdentityService? identityService = null,
        IRealTimeSyncNotifier? syncNotifier = null)
    {
        _context = context;
        _pickerDispatcher = pickerDispatcher;
        _identityService = identityService;
        _syncNotifier = syncNotifier;
    }

    public async Task<bool> Handle(SetPickerStatusCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.UserId))
            throw new DomainException("El ID de usuario es requerido.");

        if (request.BranchId == Guid.Empty)
            throw new DomainException("La sucursal es requerida.");

        if (_identityService != null)
        {
            var user = await _identityService.GetUserByIdAsync(request.UserId, cancellationToken);
            if (user == null || !RoleHelper.HasPickerRole(user.Roles))
            {
                throw new DomainException("El usuario seleccionado no cuenta con el rol de Surtidor (Picker).");
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
                // El estado Busy se gestiona principalmente por órdenes activas,
                // pero si se establece explícitamente, se refleja en pausa operativa
                userStatus.SetOperationalBreak("Ocupado en tareas especiales");
                break;
        }

        await _context.SaveChangesAsync(cancellationToken);

        if (_syncNotifier != null)
        {
            await _syncNotifier.NotifyEntityChangedAsync("PickerStatus", cancellationToken);
        }

        // Si pasó a Disponible, intentar auto-asignarle pedidos en espera de inmediato
        if (request.Status == PickerAvailabilityStatus.Available)
        {
            await _pickerDispatcher.TryAssignNextPendingOrdersToPickerAsync(request.UserId, request.BranchId, cancellationToken);
        }

        return true;
    }
}
