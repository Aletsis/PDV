namespace PDV.Domain.Enums;

public enum LogoPurpose
{
    /// <summary>Logo impreso en tickets de venta y cortes de caja.</summary>
    Ticket = 1,
    /// <summary>Logo insertado en el PDF del CFDI.</summary>
    Cfdi = 2,
    /// <summary>Logo mostrado en la interfaz web/app del POS.</summary>
    App = 3
}
