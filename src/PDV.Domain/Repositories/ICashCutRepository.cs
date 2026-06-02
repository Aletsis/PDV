using PDV.Domain.Entities;

namespace PDV.Domain.Repositories;

public interface ICashCutRepository : ICrudRepository<CashCut>
{
    /// <summary>Obtiene el corte de caja asociado a un turno específico.</summary>
    Task<CashCut?> GetByShiftIdAsync(int shiftId, CancellationToken cancellationToken = default);
    
    /// <summary>Obtiene los cortes de caja realizados por un empleado.</summary>
    Task<List<CashCut>> GetByEmployeeIdAsync(int employeeId, CancellationToken cancellationToken = default);
    
    /// <summary>Obtiene los cortes de caja de una caja registradora específica.</summary>
    Task<List<CashCut>> GetByCashRegisterIdAsync(int cashRegisterId, CancellationToken cancellationToken = default);
    
    /// <summary>Obtiene los cortes de caja de una sucursal específica.</summary>
    Task<List<CashCut>> GetByBranchIdAsync(int branchId, CancellationToken cancellationToken = default);
    
    /// <summary>Obtiene los cortes de caja en un rango de fechas.</summary>
    Task<List<CashCut>> GetByDateRangeAsync(DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default);

    /// <summary>Busca cortes de caja por múltiples criterios opcionales.</summary>
    Task<List<CashCut>> GetByCriteriaAsync(int? branchId, int? cashRegisterId, int? employeeId, DateTime? startDate, DateTime? endDate, CancellationToken cancellationToken = default);

}


