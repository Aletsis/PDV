using System;
using System.Threading;
using System.Threading.Tasks;

namespace PDV.Application.Common.Interfaces;

public interface IPickerDispatcherService
{
    /// <summary>
    /// Intenta asignar de forma inmediata y automática un pedido pendiente al surtidor disponible más idóneo de la sucursal.
    /// </summary>
    /// <param name="orderId">ID del pedido en estado Pending.</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    /// <returns>True si se asignó a un surtidor; False si no hubo surtidores disponibles (queda en cola).</returns>
    Task<bool> TryAssignPendingOrderAsync(Guid orderId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Cuando un surtidor se desocupa (completa un pedido) o se marca como disponible, intenta auto-asignarle
    /// los pedidos pendientes más antiguos (FIFO) de su sucursal hasta alcanzar su capacidad máxima.
    /// </summary>
    /// <param name="pickerUserId">ID del usuario surtidor.</param>
    /// <param name="branchId">ID de la sucursal.</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    /// <returns>Cantidad de pedidos asignados al surtidor.</returns>
    Task<int> TryAssignNextPendingOrdersToPickerAsync(string pickerUserId, Guid branchId, CancellationToken cancellationToken = default);
}
