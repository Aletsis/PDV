using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PDV.Application.Common.Interfaces;
using PDV.Domain.Entities;
using PDV.Domain.Enums;

namespace PDV.Application.Common.Services;

public class PickerDispatcherService : IPickerDispatcherService
{
    private readonly IApplicationDbContext _context;
    private readonly IRealTimeSyncNotifier? _syncNotifier;
    private readonly ILogger<PickerDispatcherService> _logger;

    public PickerDispatcherService(
        IApplicationDbContext context,
        ILogger<PickerDispatcherService> logger,
        IRealTimeSyncNotifier? syncNotifier = null)
    {
        _context = context;
        _logger = logger;
        _syncNotifier = syncNotifier;
    }

    public async Task<bool> TryAssignPendingOrderAsync(Guid orderId, CancellationToken cancellationToken = default)
    {
        try
        {
            var order = await _context.Orders
                .FirstOrDefaultAsync(o => o.Id == orderId, cancellationToken);

            if (order == null || order.Status != OrderStatus.Pending)
            {
                return false;
            }

            var config = await _context.SystemConfigurations.FirstOrDefaultAsync(cancellationToken);
            int defaultMaxOrders = config?.DefaultMaxPickingOrdersPerPicker > 0 
                ? config.DefaultMaxPickingOrdersPerPicker 
                : 1;

            // Obtener todos los estados de surtidores marcados como disponibles en la sucursal
            var candidateStatuses = await _context.UserWorkStatuses
                .Where(s => s.BranchId == order.BranchId && s.Status == PickerAvailabilityStatus.Available)
                .ToListAsync(cancellationToken);

            if (!candidateStatuses.Any())
            {
                _logger.LogInformation("No hay surtidores disponibles en la sucursal {BranchId} para el pedido {OrderId}. Permanece en cola.", order.BranchId, orderId);
                return false;
            }

            // Calcular carga activa de cada candidato
            var candidateUserIds = candidateStatuses.Select(s => s.UserId).ToList();

            var activeOrdersPerUser = await _context.Orders
                .Where(o => o.BranchId == order.BranchId && 
                            o.Status == OrderStatus.InFulfillment && 
                            o.FilledById != null && 
                            candidateUserIds.Contains(o.FilledById))
                .GroupBy(o => o.FilledById!)
                .Select(g => new { UserId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.UserId, x => x.Count, cancellationToken);

            var eligibleCandidates = candidateStatuses
                .Select(status =>
                {
                    int activeCount = activeOrdersPerUser.TryGetValue(status.UserId, out int count) ? count : 0;
                    int maxCapacity = status.MaxConcurrentOrders.HasValue && status.MaxConcurrentOrders.Value > 0
                        ? status.MaxConcurrentOrders.Value
                        : defaultMaxOrders;

                    return new
                    {
                        Status = status,
                        ActiveCount = activeCount,
                        MaxCapacity = maxCapacity,
                        RemainingSlots = maxCapacity - activeCount
                    };
                })
                .Where(c => c.RemainingSlots > 0)
                .OrderBy(c => c.ActiveCount)                              // 1° Menor carga actual
                .ThenBy(c => c.Status.OrdersCompletedToday)               // 2° Menor pedidos completados hoy
                .ThenBy(c => c.Status.LastAssignedOrderAt ?? DateTime.MinValue) // 3° Más tiempo sin recibir pedido
                .ToList();

            if (!eligibleCandidates.Any())
            {
                _logger.LogInformation("Todos los surtidores de la sucursal {BranchId} están al máximo de su capacidad para el pedido {OrderId}.", order.BranchId, orderId);
                return false;
            }

            var bestPicker = eligibleCandidates.First().Status;

            // Asignar pedido
            order.AssignPicker(bestPicker.UserId);
            bestPicker.RecordOrderAssigned();

            await _context.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Pedido {OrderId} asignado automáticamente al surtidor {PickerId} en sucursal {BranchId}.", orderId, bestPicker.UserId, order.BranchId);

            if (_syncNotifier != null)
            {
                await _syncNotifier.NotifyEntityChangedAsync("Orders", cancellationToken);
                await _syncNotifier.NotifyEntityChangedAsync("PickerStatus", cancellationToken);
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al intentar asignar pedido {OrderId} a un surtidor.", orderId);
            return false;
        }
    }

    public async Task<int> TryAssignNextPendingOrdersToPickerAsync(string pickerUserId, Guid branchId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(pickerUserId) || branchId == Guid.Empty)
            return 0;

        try
        {
            var pickerStatus = await _context.UserWorkStatuses
                .FirstOrDefaultAsync(s => s.UserId == pickerUserId && s.BranchId == branchId, cancellationToken);

            if (pickerStatus == null || pickerStatus.Status != PickerAvailabilityStatus.Available)
            {
                return 0;
            }

            var config = await _context.SystemConfigurations.FirstOrDefaultAsync(cancellationToken);
            int defaultMaxOrders = config?.DefaultMaxPickingOrdersPerPicker > 0 
                ? config.DefaultMaxPickingOrdersPerPicker 
                : 1;

            int maxCapacity = pickerStatus.MaxConcurrentOrders.HasValue && pickerStatus.MaxConcurrentOrders.Value > 0
                ? pickerStatus.MaxConcurrentOrders.Value
                : defaultMaxOrders;

            int activeCount = await _context.Orders
                .CountAsync(o => o.BranchId == branchId && 
                                 o.Status == OrderStatus.InFulfillment && 
                                 o.FilledById == pickerUserId, cancellationToken);

            int availableSlots = maxCapacity - activeCount;
            if (availableSlots <= 0)
            {
                return 0;
            }

            // Obtener los pedidos pendientes más antiguos (FIFO)
            var pendingOrders = await _context.Orders
                .Where(o => o.BranchId == branchId && o.Status == OrderStatus.Pending)
                .OrderBy(o => o.OrderDate)
                .Take(availableSlots)
                .ToListAsync(cancellationToken);

            if (!pendingOrders.Any())
            {
                return 0;
            }

            foreach (var order in pendingOrders)
            {
                order.AssignPicker(pickerUserId);
                pickerStatus.RecordOrderAssigned();
            }

            await _context.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Se auto-asignaron {Count} pedidos pendientes al surtidor {PickerId} en sucursal {BranchId}.", pendingOrders.Count, pickerUserId, branchId);

            if (_syncNotifier != null)
            {
                await _syncNotifier.NotifyEntityChangedAsync("Orders", cancellationToken);
                await _syncNotifier.NotifyEntityChangedAsync("PickerStatus", cancellationToken);
            }

            return pendingOrders.Count;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al auto-asignar pedidos pendientes al surtidor {PickerId}.", pickerUserId);
            return 0;
        }
    }
}
