using PDV.Domain.Entities;
using PDV.Domain.Enums;

namespace PDV.Domain.Repositories;

public interface IOrderRepository : ICrudRepository<Order>
{
    /// <summary>Obtiene un pedido cargando explícitamente sus ítems y desgloses.</summary>
    Task<Order?> GetByIdWithItemsAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>Obtiene pedidos asociados a un cliente específico.</summary>
    Task<List<Order>> GetByClientIdAsync(int clientId, CancellationToken cancellationToken = default);

    /// <summary>Obtiene pedidos asociados a una ruta específica.</summary>
    Task<List<Order>> GetByRouteIdAsync(string routeId, CancellationToken cancellationToken = default);

    /// <summary>Obtiene pedidos asociados a un repartidor específico.</summary>
    Task<List<Order>> GetByDeliveryManIdAsync(string deliveryManId, CancellationToken cancellationToken = default);

    /// <summary>Obtiene pedidos tomados por un empleado específico.</summary>
    Task<List<Order>> GetByTakenByIdAsync(string takenById, CancellationToken cancellationToken = default);

    /// <summary>Obtiene pedidos surtidos por un empleado específico.</summary>
    Task<List<Order>> GetByFilledByIdAsync(string filledById, CancellationToken cancellationToken = default);

    /// <summary>Obtiene pedidos capturados por un empleado específico.</summary>
    Task<List<Order>> GetByCapturedByIdAsync(string capturedById, CancellationToken cancellationToken = default);

    /// <summary>Obtiene pedidos creados en una fecha específica (solo día).</summary>
    Task<List<Order>> GetByDateAsync(DateTime date, CancellationToken cancellationToken = default);

    /// <summary>Obtiene pedidos creados en un rango de fechas.</summary>
    Task<List<Order>> GetByDateRangeAsync(DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default);

    /// <summary>Obtiene pedidos por su estatus.</summary>
    Task<List<Order>> GetByStatusAsync(OrderStatus status, CancellationToken cancellationToken = default);

    /// <summary>Obtiene pedidos por su método de pago.</summary>
    Task<List<Order>> GetByPaymentMethodAsync(PaymentMethodType paymentMethod, CancellationToken cancellationToken = default);

    /// <summary>Obtiene pedidos que tienen factura solicitada.</summary>
    Task<List<Order>> GetByInvoicedAsync(CancellationToken cancellationToken = default);

    /// <summary>Obtiene pedidos que no tienen factura solicitada.</summary>
    Task<List<Order>> GetByNotInvoicedAsync(CancellationToken cancellationToken = default);

    /// <summary>Obtiene pedidos autorizados por un supervisor.</summary>
    Task<List<Order>> GetByAuthorizedAsync(CancellationToken cancellationToken = default);

    /// <summary>Obtiene pedidos asociados a una caja registradora específica.</summary>
    Task<List<Order>> GetByCashRegisterIdAsync(int cashRegisterId, CancellationToken cancellationToken = default);

    /// <summary>Obtiene pedidos asociados a una sucursal específica.</summary>
    Task<List<Order>> GetByBranchIdAsync(int branchId, CancellationToken cancellationToken = default);

    /// <summary>Obtiene un pedido específico por su caja registradora, serie y folio.</summary>
    Task<Order?> GetByFolioAsync(int? cashRegisterId, string series, int folio, CancellationToken cancellationToken = default);

    /// <summary>Busca pedidos por múltiples criterios opcionales.</summary>
    Task<List<Order>> GetByCriteriaAsync(
        int? clientId, 
        int? cashRegisterId,
        int? branchId,
        string? series,
        int? folio,
        string? routeId, 
        string? deliveryManId, 
        string? takenById, 
        string? filledById, 
        string? capturedById, 
        DateTime? startDate, 
        DateTime? endDate, 
        OrderStatus? status, 
        PaymentMethodType? paymentMethod, 
        bool? isInvoiceRequested, 
        bool? isAuthorized, 
        CancellationToken cancellationToken = default);
}

