namespace PDV.Domain.Enums;

public enum CashRegisterMode
{
    /// <summary>
    /// Caja de piso de ventas: cobra ventas directas al público.
    /// Genera tickets de venta, devoluciones y cancellaciones.
    /// </summary>
    SalesFloor = 1,

    /// <summary>
    /// Caja de pedidos: registra y gestiona órdenes previas a la entrega.
    /// Genera notas de pedido y remisiones, las ventas se consolidan al entregar.
    /// </summary>
    Orders = 2
}
