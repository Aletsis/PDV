using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using PDV.Domain.Entities;
using PDV.Domain.Enums;

namespace PDV.Domain.Repositories;

public interface IDeliveryRouteRepository : ICrudRepository<DeliveryRoute>
{
    /// <summary>Obtiene una ruta cargando sus pedidos asociados.</summary>
    Task<DeliveryRoute?> GetByIdWithOrdersAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Obtiene las rutas asociadas a un repartidor.</summary>
    Task<List<DeliveryRoute>> GetByDeliveryManIdAsync(string deliveryManId, CancellationToken cancellationToken = default);

    /// <summary>Obtiene las rutas asociadas a una sucursal.</summary>
    Task<List<DeliveryRoute>> GetByBranchIdAsync(Guid branchId, CancellationToken cancellationToken = default);

    /// <summary>Obtiene las rutas en un estado específico.</summary>
    Task<List<DeliveryRoute>> GetByStatusAsync(DeliveryRouteStatus status, CancellationToken cancellationToken = default);

    /// <summary>Obtiene el siguiente folio secuencial de ruta para una sucursal.</summary>
    Task<int> GetNextFolioAsync(Guid branchId, CancellationToken cancellationToken = default);
}
