using PDV.Domain.Entities;
using PDV.Domain.Enums;

namespace PDV.Domain.Repositories;

public interface ICancellationRepository : IReadOnlyRepository<Cancellation>
{
    /// <summary>Obtiene las cancelaciones asociadas a una venta.</summary>
    Task<List<Cancellation>> GetBySaleIdAsync(int saleId, CancellationToken cancellationToken = default);
    
    /// <summary>Obtiene las cancelaciones realizadas por un empleado.</summary>
    Task<List<Cancellation>> GetByEmployeeIdAsync(int employeeId, CancellationToken cancellationToken = default);
    
    /// <summary>Obtiene las cancelaciones de una sucursal específica.</summary>
    Task<List<Cancellation>> GetByBranchIdAsync(int branchId, CancellationToken cancellationToken = default);
    
    /// <summary>Obtiene las cancelaciones en un rango de fechas.</summary>
    Task<List<Cancellation>> GetByDateRangeAsync(DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default);

    /// <summary>Obtiene las cancelaciones por tipo de cancelación.</summary>
    Task<List<Cancellation>> GetByTypeAsync(CancellationType type, CancellationToken cancellationToken = default);

    /// <summary>Busca cancelaciones por múltiples criterios opcionales.</summary>
    Task<List<Cancellation>> GetByCriteriaAsync(int? branchId, int? saleId, int? employeeId, CancellationType? type, DateTime? startDate, DateTime? endDate, CancellationToken cancellationToken = default);
}


