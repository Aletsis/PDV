using PDV.Domain.Entities;
using PDV.Domain.Enums;

namespace PDV.Domain.Repositories;

/// <summary>
/// Repositorio para Sale (agregado raíz)
/// </summary>
public interface ISaleRepository : IReadOnlyRepository<Sale>
{
    /// <summary>Obtiene una venta con todos sus ítems.</summary>
    Task<Sale?> GetByIdWithItemsAsync(Guid id, CancellationToken cancellationToken = default);
    
    /// <summary>Actualiza una venta (por ejemplo, para cambiar su estado).</summary>
    Task UpdateAsync(Sale sale, CancellationToken cancellationToken = default);

    /// <summary>Obtiene ventas filtradas por método de pago.</summary>
    Task<List<Sale>> GetByPaymentMethodAsync(PaymentMethodType paymentMethod, CancellationToken cancellationToken = default);

    /// <summary>Obtiene ventas realizadas por un usuario específico.</summary>
    Task<List<Sale>> GetByUserIdAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>Obtiene ventas asociadas a un turno específico.</summary>
    Task<List<Sale>> GetByShiftIdAsync(Guid shiftId, CancellationToken cancellationToken = default);

    /// <summary>Obtiene una venta específica por su caja registradora, serie y folio.</summary>
    Task<Sale?> GetByFolioAsync(Guid? cashRegisterId, string series, int folio, CancellationToken cancellationToken = default);

    /// <summary>Obtiene ventas asociadas a un cliente específico.</summary>
    Task<List<Sale>> GetByClientIdAsync(Guid clientId, CancellationToken cancellationToken = default);

    /// <summary>Obtiene ventas asociadas a una caja registradora específica.</summary>
    Task<List<Sale>> GetByCashRegisterIdAsync(Guid cashRegisterId, CancellationToken cancellationToken = default);

    /// <summary>Obtiene ventas creadas en una fecha específica (solo día).</summary>
    Task<List<Sale>> GetByDateAsync(DateTime date, CancellationToken cancellationToken = default);

    /// <summary>Obtiene ventas creadas en un rango de fechas.</summary>
    Task<List<Sale>> GetByDateRangeAsync(DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default);

    /// <summary>Obtiene ventas que han sido pagadas.</summary>
    Task<List<Sale>> GetByIsPaidAsync(CancellationToken cancellationToken = default);

    /// <summary>Obtiene ventas que no han sido pagadas.</summary>
    Task<List<Sale>> GetByIsNotPaidAsync(CancellationToken cancellationToken = default);

    /// <summary>Obtiene ventas canceladas.</summary>
    Task<List<Sale>> GetByIsCancelledAsync(CancellationToken cancellationToken = default);

    /// <summary>Obtiene ventas que han sido facturadas.</summary>
    Task<List<Sale>> GetByInvoicedAsync(CancellationToken cancellationToken = default);

    /// <summary>Obtiene ventas que no han sido facturadas.</summary>
    Task<List<Sale>> GetByNotInvoicedAsync(CancellationToken cancellationToken = default);

    /// <summary>Obtiene ventas asociadas a un ID de factura fiscal.</summary>
    Task<List<Sale>> GetByInvoiceIdAsync(string invoiceId, CancellationToken cancellationToken = default);

    /// <summary>Busca ventas por múltiples criterios opcionales.</summary>
    Task<List<Sale>> GetByCriteriaAsync(
        Guid? clientId, 
        Guid? cashRegisterId, 
        Guid? branchId, 
        Guid? shiftId, 
        string? userId, 
        PaymentMethodType? paymentMethod, 
        string? series, 
        int? folio, 
        bool? isPaid, 
        bool? isCancelled, 
        bool? isInvoiced, 
        string? invoiceId, 
        DateTime? startDate, 
        DateTime? endDate, 
        CancellationToken cancellationToken = default);
}


