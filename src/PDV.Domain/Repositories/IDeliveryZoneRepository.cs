using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using PDV.Domain.Entities;

namespace PDV.Domain.Repositories;

public interface IDeliveryZoneRepository : ICrudRepository<DeliveryZone>
{
    /// <summary>Obtiene las zonas de reparto asociadas a una sucursal.</summary>
    Task<List<DeliveryZone>> GetByBranchIdAsync(Guid branchId, CancellationToken cancellationToken = default);

    /// <summary>Obtiene las zonas de reparto activas asociadas a una sucursal.</summary>
    Task<List<DeliveryZone>> GetActiveZonesByBranchIdAsync(Guid branchId, CancellationToken cancellationToken = default);
}
