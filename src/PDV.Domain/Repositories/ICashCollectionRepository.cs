using PDV.Domain.Entities;

namespace PDV.Domain.Repositories;

public interface ICashCollectionRepository : ICrudRepository<CashCollection>
{
    /// <summary>Obtiene las recolecciones asociadas a un turno.</summary>
    Task<List<CashCollection>> GetByShiftIdAsync(int shiftId, CancellationToken cancellationToken = default);
    
    /// <summary>Obtiene las recolecciones de una caja registradora específica.</summary>
    Task<List<CashCollection>> GetByCashRegisterIdAsync(int cashRegisterId, CancellationToken cancellationToken = default);
    
    /// <summary>Obtiene las recolecciones de una sucursal específica (vía caja registradora).</summary>
    Task<List<CashCollection>> GetByBranchIdAsync(int branchId, CancellationToken cancellationToken = default);
    
    /// <summary>Obtiene las recolecciones realizadas por un empleado.</summary>
    Task<List<CashCollection>> GetByEmployeeIdAsync(int employeeId, CancellationToken cancellationToken = default);
    
    /// <summary>Obtiene las recolecciones en un rango de fechas.</summary>
    Task<List<CashCollection>> GetByDateRangeAsync(DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default);

    /// <summary>Busca recolecciones por múltiples criterios opcionales.</summary>
    Task<List<CashCollection>> GetByCriteriaAsync(int? branchId, int? shiftId, int? cashRegisterId, int? employeeId, DateTime? startDate, DateTime? endDate, CancellationToken cancellationToken = default);
}

