using PDV.Domain.Entities;
using PDV.Domain.Enums;

namespace PDV.Domain.Repositories;

public interface ICashRegisterRepository : ICrudRepository<CashRegister>
{
    /// <summary>Obtiene todas las cajas de una sucursal específica.</summary>
    Task<List<CashRegister>> GetByBranchAsync(int branchId, bool includeInactive = false, CancellationToken cancellationToken = default);
    
    /// <summary>Busca cajas por modo (Piso de Ventas / Pedidos).</summary>
    Task<List<CashRegister>> GetByModeAsync(CashRegisterMode mode, CancellationToken cancellationToken = default);
    
    /// <summary>Obtiene todas las cajas activas.</summary>
    Task<List<CashRegister>> GetAllActiveAsync(CancellationToken cancellationToken = default);
    
    /// <summary>Obtiene todas las cajas inactivas.</summary>
    Task<List<CashRegister>> GetAllInactiveAsync(CancellationToken cancellationToken = default);
}