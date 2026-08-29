namespace PDV.Domain.Enums;

public enum OrderChannel
{
    /// <summary>Llamada telefónica tradicional.</summary>
    Telephone = 1,

    /// <summary>Mensajería de WhatsApp.</summary>
    WhatsApp = 2,

    /// <summary>Mostrador / Presencial en sucursal.</summary>
    Store = 3,

    /// <summary>Sitio Web / Portal de Clientes.</summary>
    Web = 4,

    /// <summary>Aplicación Móvil.</summary>
    MobileApp = 5,

    /// <summary>Otro canal de recepción.</summary>
    Other = 99
}
