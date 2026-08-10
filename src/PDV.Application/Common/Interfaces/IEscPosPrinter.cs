namespace PDV.Application.Common.Interfaces;

public interface IEscPosPrinter
{
    /// <summary>
    /// Verifica la conectividad y disponibilidad de la impresora (Online / Offline).
    /// </summary>
    Task<bool> CheckStatusAsync(string ipAddress, int port, CancellationToken cancellationToken = default);

    /// <summary>
    /// Ejecuta un trabajo de impresión estructurado a través del pipeline avanzado con control de corte, gaveta y copias.
    /// </summary>
    Task PrintJobAsync(
        string ipAddress,
        int port,
        string text,
        bool autoCut = true,
        bool partialCut = true,
        bool openDrawerBefore = false,
        bool openDrawerAfter = false,
        int copies = 1,
        int? encodingCodePage = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Envía texto simple al punto de impresión ESC/POS en la impresora.
    /// </summary>
    Task PrintTextAsync(string ipAddress, int port, string text, int? encodingCodePage = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Envía bytes raw ESC/POS a la impresora.
    /// </summary>
    Task PrintRawAsync(string ipAddress, int port, byte[] data, CancellationToken cancellationToken = default);

    Task PrintImageAsync(string ipAddress, int port, byte[] imagePngBytes, int maxWidth = 384, CancellationToken cancellationToken = default);

    Task PrintBarcodeAsync(string ipAddress, int port, string data, int barcodeType = 73, int height = 100, CancellationToken cancellationToken = default);

    Task PrintQrAsync(string ipAddress, int port, string data, int moduleSize = 4, int errorLevel = 48, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Envía el comando de pulso eléctrico para abrir el cajón de dinero conectado a la impresora.
    /// </summary>
    Task OpenDrawerAsync(string ipAddress, int port, CancellationToken cancellationToken = default);
}
