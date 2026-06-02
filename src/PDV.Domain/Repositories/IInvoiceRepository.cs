using PDV.Domain.Entities;
using PDV.Domain.Enums;

namespace PDV.Domain.Repositories;

public interface IInvoiceRepository : ICrudRepository<Invoice>
{
    /// <summary>Busca una factura por su UUID.</summary>
    Task<Invoice?> GetByUuidAsync(string uuid, CancellationToken cancellationToken = default);
    
    /// <summary>Busca una factura por su folio y serie.</summary>
    Task<Invoice?> GetByFolioAndSeriesAsync(string series, string folio, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Obtiene la factura ligada a una venta específica.
    /// </summary>
    Task<Invoice?> GetBySaleIdAsync(int saleId, CancellationToken cancellationToken = default);

    /// <summary>Obtiene facturas por tipo.</summary>
    Task<List<Invoice>> GetByTypeAsync(InvoiceType type, CancellationToken cancellationToken = default);

    /// <summary>Obtiene facturas por estatus.</summary>
    Task<List<Invoice>> GetByStatusAsync(InvoiceStatus status, CancellationToken cancellationToken = default);

    /// <summary>Obtiene facturas creadas en una fecha específica (solo día).</summary>
    Task<List<Invoice>> GetByDateAsync(DateTime date, CancellationToken cancellationToken = default);

    /// <summary>Obtiene facturas creadas en un rango de fechas.</summary>
    Task<List<Invoice>> GetByDateRangeAsync(DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default);

    /// <summary>Obtiene facturas timbradas en un rango de fechas.</summary>
    Task<List<Invoice>> GetByStampedDateRangeAsync(DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default);

    /// <summary>Obtiene facturas canceladas en un rango de fechas.</summary>
    Task<List<Invoice>> GetByCancelledDateRangeAsync(DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default);

    /// <summary>Obtiene facturas de una sucursal específica.</summary>
    Task<List<Invoice>> GetByBranchIdAsync(int branchId, CancellationToken cancellationToken = default);

    /// <summary>Obtiene facturas asociadas a un cliente específico.</summary>
    Task<List<Invoice>> GetByClientIdAsync(int clientId, CancellationToken cancellationToken = default);

    /// <summary>Obtiene facturas asociadas a un turno específico.</summary>
    Task<List<Invoice>> GetByShiftIdAsync(int shiftId, CancellationToken cancellationToken = default);

    /// <summary>Obtiene facturas asociadas a una devolución específica (Nota de crédito).</summary>
    Task<List<Invoice>> GetByReturnIdAsync(int returnId, CancellationToken cancellationToken = default);

    /// <summary>Busca facturas por múltiples criterios opcionales.</summary>
    Task<List<Invoice>> GetByCriteriaAsync(
        int? branchId, 
        int? clientId, 
        int? shiftId, 
        int? returnId, 
        InvoiceType? type, 
        InvoiceStatus? status, 
        DateTime? startDate, 
        DateTime? endDate, 
        string? folio, 
        string? series, 
        string? uuid, 
        CancellationToken cancellationToken = default);
}

