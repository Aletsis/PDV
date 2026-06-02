namespace PDV.Domain.Enums;

public enum PrinterConnectionType
{
    /// <summary>Impresora conectada por red TCP/IP (requiere IpAddress y Port).</summary>
    Network = 1,
    /// <summary>Impresora conectada por puerto USB o LPT (requiere DevicePath).</summary>
    Usb = 2,
    /// <summary>Impresora conectada por puerto serial RS-232.</summary>
    Serial = 3,
    /// <summary>Impresora Bluetooth.</summary>
    Bluetooth = 4
}
