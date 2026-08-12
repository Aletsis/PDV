namespace PDV.Domain.Enums;

/// <summary>
/// Representa el resultado o estatus de la conciliación del corte de caja.
/// </summary>
public enum ReconciliationStatus
{
    /// <summary>
    /// Los montos de efectivo y váuchers coinciden con lo esperado por el sistema.
    /// </summary>
    Balanced = 1,

    /// <summary>
    /// Existe faltante de efectivo entregado.
    /// </summary>
    CashShortage = 2,

    /// <summary>
    /// Existe sobrante de efectivo entregado.
    /// </summary>
    CashSurplus = 3,

    /// <summary>
    /// Existe faltante de váuchers con tarjeta.
    /// </summary>
    VoucherShortage = 4,

    /// <summary>
    /// Existe sobrante de váuchers con tarjeta.
    /// </summary>
    VoucherSurplus = 5,

    /// <summary>
    /// Existen diferencias mixtas tanto en efectivo como en váuchers.
    /// </summary>
    Discrepancy = 6
}
