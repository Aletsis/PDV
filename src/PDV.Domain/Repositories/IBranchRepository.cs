using PDV.Domain.Entities;

namespace PDV.Domain.Repositories;

public interface IBranchRepository : ICrudRepository<Branch>
{
    /// <summary>Obtiene una sucursal por su código.</summary>
    Task<Branch?> GetByCodeAsync(string code, CancellationToken cancellationToken = default);
    
    /// <summary>Obtiene todas las sucursales activas.</summary>
    Task<List<Branch>> GetAllActiveAsync(CancellationToken cancellationToken = default);
    
    /// <summary>Obtiene todas las sucursales inactivas.</summary>
    Task<List<Branch>> GetAllInactiveAsync(CancellationToken cancellationToken = default);
    
    /// <summary>Obtiene la sucursal principal.</summary>
    Task<Branch?> GetMainBranchAsync(CancellationToken cancellationToken = default);
}

