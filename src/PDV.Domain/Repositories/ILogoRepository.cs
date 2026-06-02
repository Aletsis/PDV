using PDV.Domain.Entities;
using PDV.Domain.Enums;

namespace PDV.Domain.Repositories;

public interface ILogoRepository : ICrudRepository<Logo>
{
    /// <summary>Obtiene el logo activo para un propósito específico y opcionalmente una sucursal.</summary>
    Task<Logo?> GetActiveLogoAsync(LogoPurpose purpose, int? branchId, CancellationToken cancellationToken = default);

    /// <summary>Obtiene todos los logos asociados a una sucursal específica.</summary>
    Task<List<Logo>> GetByBranchIdAsync(int? branchId, CancellationToken cancellationToken = default);

    /// <summary>Obtiene todos los logos por propósito.</summary>
    Task<List<Logo>> GetByPurposeAsync(LogoPurpose purpose, CancellationToken cancellationToken = default);
}
