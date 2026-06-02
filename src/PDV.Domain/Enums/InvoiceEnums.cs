namespace PDV.Domain.Enums;

public enum InvoiceType
{
    /// <summary>Factura emitida a un cliente con RFC real. Ligada a una Venta.</summary>
    Customer = 1,
    /// <summary>Factura global emitida a XAXX010101000. Ligada a un Turno.</summary>
    Global = 2,
    /// <summary>Nota de crédito (CFDI de Egreso). Ligada a una Devolución o descuento.</summary>
    CreditNote = 3
}

public enum InvoiceStatus
{
    /// <summary>Creada en sistema, pendiente de timbrado.</summary>
    Draft = 1,
    /// <summary>Timbrada exitosamente ante el SAT (tiene UUID).</summary>
    Stamped = 2,
    /// <summary>Anulada solo en sistema interno (nunca se timbró o el PAC no responde). No tiene efecto fiscal ante el SAT.</summary>
    VoidedInSystem = 3,
    /// <summary>Cancelada formalmente ante el SAT con UUID de cancelación.</summary>
    CancelledAtSat = 4
}

public enum CfdiUsage
{
    GeneralExpense = 1,    // G03 - Gastos en general
    Acquisition = 2,        // I01 - Adquisición de mercancias
    ToDefine = 3            // S01 - Sin efectos fiscales (facturas globales)
}

/// <summary>
/// Motivos oficiales del SAT para la cancelación de un CFDI (Catálogo c_MotivoCancelacion).
/// </summary>
public enum SatCancellationMotif
{
    /// <summary>01 — Comprobante emitido con errores con relación.
    /// Requiere UUID del CFDI sustituto.</summary>
    ErrorWithRelation = 1,

    /// <summary>02 — Comprobante emitido con errores sin relación.
    /// No requiere CFDI sustituto.</summary>
    ErrorWithoutRelation = 2,

    /// <summary>03 — No se llevó a cabo la operación.</summary>
    OperationNotCarriedOut = 3,

    /// <summary>04 — Operación nominativa relacionada en una factura global.</summary>
    NominativeOperationInGlobal = 4
}
