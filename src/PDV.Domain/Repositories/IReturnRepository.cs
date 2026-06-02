using PDV.Domain.Entities;
using PDV.Domain.Enums;

namespace PDV.Domain.Repositories;

public interface IReturnRepository : IReadOnlyRepository<Return>
{
    /// <summary>Obtiene una devolución cargando explícitamente sus ítems y desgloses.</summary>
    Task<Return?> GetByIdWithItemsAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>Obtiene las devoluciones asociadas a una venta.</summary>
    Task<List<Return>> GetBySaleIdAsync(int saleId, CancellationToken cancellationToken = default);

    /// <summary>Obtiene devoluciones creadas en una fecha específica (solo día).</summary>
    Task<List<Return>> GetByDateAsync(DateTime date, CancellationToken cancellationToken = default);

    /// <summary>Obtiene devoluciones en un rango de fechas.</summary>
    Task<List<Return>> GetByDateRangeAsync(DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default);

    /// <summary>Obtiene devoluciones por método de reembolso.</summary>
    Task<List<Return>> GetByRefundMethodAsync(RefundMethod refundMethod, CancellationToken cancellationToken = default);

    /// <summary>Obtiene devoluciones por turno.</summary>
    Task<List<Return>> GetByShiftIdAsync(int shiftId, CancellationToken cancellationToken = default);

    /// <summary>Obtiene devoluciones por caja registradora.</summary>
    Task<List<Return>> GetByCashRegisterIdAsync(int cashRegisterId, CancellationToken cancellationToken = default);

    /// <summary>Obtiene devoluciones por cliente.</summary>
    Task<List<Return>> GetByClientIdAsync(int clientId, CancellationToken cancellationToken = default);

    /// <summary>Obtiene devoluciones por empleado que las autoriza.</summary>
    Task<List<Return>> GetByEmployeeIdAsync(int employeeId, CancellationToken cancellationToken = default);

    /// <summary>Obtiene devoluciones según su estado de finalización (completada o parcial/pendiente).</summary>
    Task<List<Return>> GetByCompletionStatusAsync(bool isCompleted, CancellationToken cancellationToken = default);

    /// <summary>Obtiene una devolución específica por su caja registradora, serie y folio.</summary>
    Task<Return?> GetByFolioAsync(int? cashRegisterId, string series, int folio, CancellationToken cancellationToken = default);

    /// <summary>Busca devoluciones por múltiples criterios opcionales.</summary>
    Task<List<Return>> GetByCriteriaAsync(
        int? saleId, 
        int? shiftId, 
        int? cashRegisterId, 
        int? clientId, 
        int? employeeId, 
        RefundMethod? refundMethod, 
        bool? isCompleted, 
        string? series,
        int? folio,
        DateTime? startDate, 
        DateTime? endDate, 
        CancellationToken cancellationToken = default);

    /// <summary>Actualiza una devolución existente (por ejemplo, para marcarla como completada).</summary>
    Task UpdateAsync(Return @return, CancellationToken cancellationToken = default);
}

