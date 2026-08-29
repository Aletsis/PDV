namespace PDV.Domain.Enums;

public enum OrderStatus
{
    Pending = 0,        // Pendiente de surtido (capturado por telefonista o borrador)
    InFulfillment = 1,  // En proceso de surtido por el surtidor
    Filled = 2,         // Surtido completo, listo para verificación
    Confirmed = 3,      // Verificado físicamente y confirmado en caja
    Routed = 4,         // Asignado a ruta de reparto
    EnRoute = 5,        // En camino con el repartidor
    Delivered = 6,      // Entregado con éxito
    Returned = 7,       // No entregado / Devuelto (con motivo)
    Settled = 8,        // Liquidado en sucursal
    Cancelled = 9       // Cancelado
}

