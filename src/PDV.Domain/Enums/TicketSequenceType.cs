namespace PDV.Domain.Enums;

public enum TicketSequenceType
{
    /// <summary>Nota de venta / ticket de cobro.</summary>
    Sale = 1,
    /// <summary>Nota de devolución.</summary>
    Return = 2,
    /// <summary>Nota de cancelación de venta.</summary>
    Cancellation = 3,
    /// <summary>Recibo de retiro de efectivo (arqueo parcial).</summary>
    CashCollection = 4,
    /// <summary>Reporte de corte de caja (Z-report).</summary>
    CashCut = 5,
    /// <summary>Pedido a domicilio / especial.</summary>
    Order = 6
}
