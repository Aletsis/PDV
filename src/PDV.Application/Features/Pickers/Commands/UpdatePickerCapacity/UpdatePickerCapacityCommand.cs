using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PDV.Application.Common.Interfaces;
using PDV.Domain.Entities;
using PDV.Domain.Enums;
using PDV.Domain.Exceptions;

namespace PDV.Application.Features.Pickers.Commands.UpdatePickerCapacity;

public record UpdatePickerCapacityCommand : IRequest<bool>
{
    public string UserId { get; set; } = string.Empty;
    public Guid BranchId { get; set; }
    public int? MaxConcurrentOrders { get; set; }
}

public class UpdatePickerCapacityCommandHandler : IRequestHandler<UpdatePickerCapacityCommand, bool>
{
    private readonly IApplicationDbContext _context;
    private readonly IPickerDispatcherService _pickerDispatcher;
    private readonly IRealTimeSyncNotifier? _syncNotifier;

    public UpdatePickerCapacityCommandHandler(
        IApplicationDbContext context,
        IPickerDispatcherService pickerDispatcher,
        IRealTimeSyncNotifier? syncNotifier = null)
    {
        _context = context;
        _pickerDispatcher = pickerDispatcher;
        _syncNotifier = syncNotifier;
    }

    public async Task<bool> Handle(UpdatePickerCapacityCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.UserId))
            throw new DomainException("El ID de usuario es requerido.");

        var userStatus = await _context.UserWorkStatuses
            .FirstOrDefaultAsync(s => s.UserId == request.UserId, cancellationToken);

        if (userStatus == null)
        {
            userStatus = new UserWorkStatus(request.UserId, request.BranchId, request.MaxConcurrentOrders);
            _context.UserWorkStatuses.Add(userStatus);
        }
        else
        {
            userStatus.SetCustomCapacity(request.MaxConcurrentOrders);
        }

        await _context.SaveChangesAsync(cancellationToken);

        if (_syncNotifier != null)
        {
            await _syncNotifier.NotifyEntityChangedAsync("PickerStatus", cancellationToken);
        }

        // Si el surtidor está disponible, intentar asignarle pedidos con su nueva capacidad
        if (userStatus.Status == PickerAvailabilityStatus.Available)
        {
            await _pickerDispatcher.TryAssignNextPendingOrdersToPickerAsync(request.UserId, userStatus.BranchId, cancellationToken);
        }

        return true;
    }
}
