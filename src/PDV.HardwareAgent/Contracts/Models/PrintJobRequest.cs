using PDV.HardwareAgent.Contracts.Enums;

namespace PDV.HardwareAgent.Contracts.Models;

public class PrintJobRequest
{
    /// <summary>
    /// Destino de la impresora: "tcp://192.168.1.50:9100", "usb://POS-80", "serial://COM3?baud=9600"
    /// </summary>
    public string Target { get; set; } = string.Empty;

    /// <summary>
    /// Perfil de comandos (EscPos, StarPrnt, GenericThermal)
    /// </summary>
    public PrinterProfileType Profile { get; set; } = PrinterProfileType.EscPos;

    /// <summary>
    /// Tipo de contenido
    /// </summary>
    public PrintJobContentType ContentType { get; set; } = PrintJobContentType.Text;

    /// <summary>
    /// Contenido de la carga: Texto plano, Base64 de bytes RAW, Base64 de imagen PNG/JPG, o cadena para código de barras/QR
    /// </summary>
    public string Data { get; set; } = string.Empty;

    /// <summary>
    /// Codificación de caracteres (por defecto 1252 / Latin-1)
    /// </summary>
    public int CodePage { get; set; } = 1252;

    /// <summary>
    /// Ancho máximo en puntos para imágenes (por defecto 384 para 80mm)
    /// </summary>
    public int MaxWidth { get; set; } = 384;

    /// <summary>
    /// Altura en puntos para códigos de barras
    /// </summary>
    public int BarcodeHeight { get; set; } = 100;

    /// <summary>
    /// Tipo de código de barras (por defecto 73 = CODE128)
    /// </summary>
    public int BarcodeType { get; set; } = 73;

    /// <summary>
    /// Tamaño de módulo para códigos QR (1..16, default 4)
    /// </summary>
    public int QrModuleSize { get; set; } = 4;

    /// <summary>
    /// Nivel de corrección de error QR (48..51 = L, M, Q, H, default 48)
    /// </summary>
    public int QrErrorLevel { get; set; } = 48;

    /// <summary>
    /// Indica si debe enviar comando de corte automático al finalizar
    /// </summary>
    public bool AutoCut { get; set; } = true;

    /// <summary>
    /// Indica si el corte es parcial (true) o total (false)
    /// </summary>
    public bool PartialCut { get; set; } = true;

    /// <summary>
    /// Abrir cajón de dinero antes de imprimir
    /// </summary>
    public bool OpenDrawerBefore { get; set; } = false;

    /// <summary>
    /// Abrir cajón de dinero después de imprimir
    /// </summary>
    public bool OpenDrawerAfter { get; set; } = false;

    /// <summary>
    /// Número de copias (1..5)
    /// </summary>
    public int Copies { get; set; } = 1;

    /// <summary>
    /// Número máximo de reintentos en caso de error transitorio
    /// </summary>
    public int MaxRetries { get; set; } = 3;

    /// <summary>
    /// Timeout en milisegundos para la conexión y envío
    /// </summary>
    public int TimeoutMs { get; set; } = 5000;
}
