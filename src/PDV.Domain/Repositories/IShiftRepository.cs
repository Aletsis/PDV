using PDV.Domain.Entities;
using PDV.Domain.Enums;

namespace PDV.Domain.Repositories;

public interface IShiftRepository : IReadOnlyRepository<Shift>
{
    /// <summary>
    /// Obtiene el turno activo (abierto) para una caja específica.
    /// Solo puede haber un turno abierto por caja a la vez.
    /// </summary>
    Task<Shift?> GetActiveShiftAsync(int cashRegisterId, CancellationToken cancellationToken = default);

    /// <summary>Actualiza los totales y el estado de un turno.</summary>
    Task UpdateAsync(Shift shift, CancellationToken cancellationToken = default);

    /// <summary>Obtiene los turnos asociados a una caja registradora específica.</summary>
    Task<List<Shift>> GetByCashRegisterIdAsync(int cashRegisterId, CancellationToken cancellationToken = default);

    /// <summary>Obtiene los turnos asociados a un cajero/usuario específico.</summary>
    Task<List<Shift>> GetByUserIdAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>Obtiene los turnos por su estado (Abierto, Cerrado, etc.).</summary>
    Task<List<Shift>> GetByStatusAsync(ShiftStatus status, CancellationToken cancellationToken = default);

    /// <summary>Obtiene los turnos creados en una fecha específica (solo día).</summary>
    Task<List<Shift>> GetByDateAsync(DateTime date, CancellationToken cancellationToken = default);

    /// <summary>Obtiene los turnos creados en un rango de fechas.</summary>
    Task<List<Shift>> GetByDateRangeAsync(DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default);

    /// <summary>Obtiene turnos que requieren factura global pero que no han sido facturados.</summary>
    Task<List<Shift>> GetPendingGlobalInvoiceAsync(CancellationToken cancellationToken = default);

    /// <summary>Obtiene el último turno cerrado de una caja registradora específica.</summary>
    Task<Shift?> GetLastClosedShiftAsync(int cashRegisterId, CancellationToken cancellationToken = default);

    /// <summary>Obtiene los turnos filtrados por su estado de consolidación.</summary>
    Task<List<Shift>> GetByConsolidationStatusAsync(bool isConsolidated, CancellationToken cancellationToken = default);

    /// <summary>Busca turnos por múltiples criterios opcionales.</summary>
    Task<List<Shift>> GetByCriteriaAsync(
        int? cashRegisterId, 
        int? branchId, 
        string? userId, 
        ShiftStatus? status, 
        bool? isGlobalInvoiceRequested, 
        bool? isGlobalInvoiced, 
        bool? isConsolidated,
        DateTime? startDate, 
        DateTime? endDate, 
        CancellationToken cancellationToken = default);
}

