namespace PDV.Domain.Enums;

public enum SaleType
{
    /// <summary>Vendido por pieza/unidad.</summary>
    Piece = 0,
    /// <summary>Vendido a granel (por peso o volumen).</summary>
    Bulk = 1
}

public enum TaxRateType
{
    /// <summary>Exento de IVA.</summary>
    Exempt = 0,
    /// <summary>IVA tasa 0%.</summary>
    ZeroRate = 1,
    /// <summary>IVA tasa 8% (zona fronteriza).</summary>
    Rate8 = 2,
    /// <summary>IVA tasa 16% (tasa general).</summary>
    Rate16 = 3
}

public enum ProductType
{
    Producto = 1,    // 1: Producto estándar
    Paquete = 2,     // 2: Paquete
    Servicio = 3     // 3: Servicio
}

public enum ControlExistencia
{
    SinControl = 1,                 // Sin control de existencia
    ConControl = 2,                 // Con control de existencia (estándar)
    UnidadesDeMedidaYPeso = 3,      // Control por unidades/peso
    Caracteristicas = 4,            // Control por características
    Series = 5,                     // Control por número de serie
    Pedimentos = 6,                 // Control por pedimentos
    Lotes = 7                       // Control por lotes
}

