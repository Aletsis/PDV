#pragma warning disable CA1416

using System.Drawing;
using System.IO.Ports;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Web;
using PDV.Application.Common.Interfaces;

namespace PDV.Infrastructure.Printing;

public class MultiChannelEscPosPrinter : IEscPosPrinter
{
    private readonly Encoding _defaultEncoding = Encoding.GetEncoding(1252);

    private Encoding ChooseEncoding(string text, int? codePage)
    {
        if (codePage.HasValue)
        {
            try { return Encoding.GetEncoding(codePage.Value); } catch { }
        }
        if (text.Any(c => c > 127)) return _defaultEncoding;
        return Encoding.ASCII;
    }

    public async Task PrintTextAsync(string ipAddress, int port, string text, int? encodingCodePage = null, CancellationToken cancellationToken = default)
    {
        var sb = new List<byte>();
        sb.AddRange(new byte[] { 0x1B, 0x40 }); // Init

        var encoding = ChooseEncoding(text, encodingCodePage);
        sb.AddRange(encoding.GetBytes(text));
        sb.AddRange(new byte[] { 0x0A }); // LF

        sb.AddRange(new byte[] { 0x1B, 0x64, 0x03 }); // Feed 3 lines
        sb.AddRange(new byte[] { 0x1D, 0x56, 0x00 }); // Full cut

        await PrintRawAsync(ipAddress, port, sb.ToArray(), cancellationToken);
    }

    public async Task PrintImageAsync(string ipAddress, int port, byte[] imagePngBytes, int maxWidth = 384, CancellationToken cancellationToken = default)
    {
        using var ms = new MemoryStream(imagePngBytes);
        using var bmp = new Bitmap(ms);

        var width = bmp.Width;
        var height = bmp.Height;
        if (width > maxWidth)
        {
            var ratio = (double)maxWidth / width;
            width = maxWidth;
            height = (int)(height * ratio);
        }

        using var resized = new Bitmap(bmp, new Size(width, height));
        var imgBytes = ConvertBitmapToRaster(resized);

        var header = new List<byte>();
        header.AddRange(new byte[] { 0x1B, 0x40 }); // Init

        int xL = width % 256;
        int xH = width / 256;
        int yL = height % 256;
        int yH = height / 256;

        header.AddRange(new byte[] { 0x1D, 0x76, 0x30, 0x00, (byte)xL, (byte)xH, (byte)yL, (byte)yH });
        header.AddRange(imgBytes);
        header.AddRange(new byte[] { 0x0A, 0x1B, 0x64, 0x02, 0x1D, 0x56, 0x00 }); // Cut

        await PrintRawAsync(ipAddress, port, header.ToArray(), cancellationToken);
    }

    private byte[] ConvertBitmapToRaster(Bitmap bmp)
    {
        int width = bmp.Width;
        int height = bmp.Height;
        int bytesPerLine = (width + 7) / 8;
        var data = new byte[bytesPerLine * height];

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                var color = bmp.GetPixel(x, y);
                int luminance = (int)(0.299 * color.R + 0.587 * color.G + 0.114 * color.B);
                if (luminance < 127) // Black threshold
                {
                    int index = y * bytesPerLine + x / 8;
                    data[index] |= (byte)(0x80 >> (x % 8));
                }
            }
        }
        return data;
    }

    public async Task PrintBarcodeAsync(string ipAddress, int port, string data, int barcodeType = 73, int height = 100, CancellationToken cancellationToken = default)
    {
        var cmd = new List<byte>();
        cmd.AddRange(new byte[] { 0x1B, 0x40 }); // Init
        cmd.AddRange(new byte[] { 0x1D, 0x48, 0x02 }); // HRI below
        cmd.AddRange(new byte[] { 0x1D, 0x68, (byte)height });
        cmd.AddRange(new byte[] { 0x1D, 0x6B, (byte)barcodeType });
        cmd.AddRange(Encoding.ASCII.GetBytes(data));
        cmd.Add(0x00);
        cmd.AddRange(new byte[] { 0x0A, 0x1D, 0x56, 0x00 }); // Cut

        await PrintRawAsync(ipAddress, port, cmd.ToArray(), cancellationToken);
    }

    public async Task PrintQrAsync(string ipAddress, int port, string data, int moduleSize = 4, int errorLevel = 48, CancellationToken cancellationToken = default)
    {
        var list = new List<byte>();
        list.AddRange(new byte[] { 0x1B, 0x40 }); // Init
        list.AddRange(new byte[] { 0x1D, 0x28, 0x6B, 0x04, 0x00, 0x31, 0x41, 0x32, 0x00 }); // Model 2
        list.AddRange(new byte[] { 0x1D, 0x28, 0x6B, 0x03, 0x00, 0x31, 0x43, (byte)moduleSize }); // Size
        list.AddRange(new byte[] { 0x1D, 0x28, 0x6B, 0x03, 0x00, 0x31, 0x45, (byte)errorLevel }); // EC Level

        var bytes = Encoding.UTF8.GetBytes(data);
        int pL = (bytes.Length + 3) % 256;
        int pH = (bytes.Length + 3) / 256;
        list.AddRange(new byte[] { 0x1D, 0x28, 0x6B, (byte)pL, (byte)pH, 0x31, 0x50, 0x30 });
        list.AddRange(bytes);
        list.AddRange(new byte[] { 0x1D, 0x28, 0x6B, 0x03, 0x00, 0x31, 0x51, 0x30 }); // Print QR
        list.AddRange(new byte[] { 0x0A, 0x1B, 0x64, 0x02, 0x1D, 0x56, 0x00 }); // Cut

        await PrintRawAsync(ipAddress, port, list.ToArray(), cancellationToken);
    }

    public async Task OpenDrawerAsync(string ipAddress, int port, CancellationToken cancellationToken = default)
    {
        var cmd = new byte[] { 0x1B, 0x70, 0x00, 0x19, 0xFA }; // ESC p 0 25 250
        await PrintRawAsync(ipAddress, port, cmd, cancellationToken);
    }

    public async Task PrintRawAsync(string connectionUri, int port, byte[] data, CancellationToken cancellationToken = default)
    {
        // Enforce fallback to TCP if it looks like a plain IP address (for backwards compatibility)
        var targetUri = connectionUri;
        if (!connectionUri.Contains("://"))
        {
            var targetPort = port <= 0 ? 9100 : port;
            targetUri = $"tcp://{connectionUri}:{targetPort}";
        }

        var uri = new Uri(targetUri);
        var scheme = uri.Scheme.ToLowerInvariant();

        switch (scheme)
        {
            case "tcp":
                await PrintTcpAsync(uri.Host, uri.Port, data, cancellationToken);
                break;
            case "serial":
                await PrintSerialAsync(uri, data, cancellationToken);
                break;
            case "usb":
                await PrintUsbAsync(uri, data, cancellationToken);
                break;
            default:
                throw new NotSupportedException($"Esquema de conexión '{scheme}' no soportado para impresión.");
        }
    }

    private async Task PrintTcpAsync(string ipAddress, int port, byte[] data, CancellationToken cancellationToken)
    {
        using var client = new TcpClient();
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        await client.ConnectAsync(ipAddress, port, cts.Token).ConfigureAwait(false);
        using var stream = client.GetStream();
        await stream.WriteAsync(data, 0, data.Length, cts.Token).ConfigureAwait(false);
        await stream.FlushAsync(cts.Token).ConfigureAwait(false);
        await Task.Delay(100, cts.Token).ConfigureAwait(false);
    }

    private async Task PrintSerialAsync(Uri uri, byte[] data, CancellationToken cancellationToken)
    {
        // serial://COM3?baud=9600
        // serial:///dev/ttyS0?baud=9600
        var portName = uri.Host;
        if (string.IsNullOrEmpty(portName))
        {
            // If triple slash is used (unix dev path: serial:///dev/ttyS0) Host is empty and path contains the name
            portName = HttpUtility.UrlDecode(uri.AbsolutePath);
        }

        var baudRate = 9600;
        var query = HttpUtility.ParseQueryString(uri.Query);
        if (int.TryParse(query["baud"] ?? query["baudrate"], out var parsedBaud))
        {
            baudRate = parsedBaud;
        }

        await Task.Run(() =>
        {
            using var serialPort = new SerialPort(portName, baudRate, Parity.None, 8, StopBits.One);
            serialPort.Open();
            serialPort.Write(data, 0, data.Length);
            // Give serial transmission time to complete before closing port
            Thread.Sleep(200);
        }, cancellationToken);
    }

    private async Task PrintUsbAsync(Uri uri, byte[] data, CancellationToken cancellationToken)
    {
        // usb://GenericPrinterName (Windows)
        // usb:///dev/usb/lp0 (Linux/macOS)
        var printerName = uri.Host;
        if (string.IsNullOrEmpty(printerName))
        {
            printerName = HttpUtility.UrlDecode(uri.AbsolutePath);
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            await Task.Run(() =>
            {
                var success = RawPrinterHelper.SendBytesToPrinter(printerName, data);
                if (!success)
                {
                    throw new IOException($"No se pudo escribir en la impresora USB de Windows: '{printerName}'");
                }
            }, cancellationToken);
        }
        else
        {
            // Linux/macOS raw file system USB writing
            await Task.Run(async () =>
            {
                using var fs = new FileStream(printerName, FileMode.Open, FileAccess.Write);
                await fs.WriteAsync(data, 0, data.Length, cancellationToken);
                await fs.FlushAsync(cancellationToken);
            }, cancellationToken);
        }
    }
}

// Windows native printing helper class
internal static class RawPrinterHelper
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
    public class DOCINFOA
    {
        [MarshalAs(UnmanagedType.LPStr)] public string? pDocName;
        [MarshalAs(UnmanagedType.LPStr)] public string? pOutputFile;
        [MarshalAs(UnmanagedType.LPStr)] public string? pDataType;
    }

    [DllImport("winspool.Drv", EntryPoint = "OpenPrinterA", SetLastError = true, CharSet = CharSet.Ansi, ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
    public static extern bool OpenPrinter([MarshalAs(UnmanagedType.LPStr)] string szPrinter, out IntPtr hPrinter, IntPtr pd);

    [DllImport("winspool.Drv", EntryPoint = "ClosePrinter", SetLastError = true, ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
    public static extern bool ClosePrinter(IntPtr hPrinter);

    [DllImport("winspool.Drv", EntryPoint = "StartDocPrinterA", SetLastError = true, CharSet = CharSet.Ansi, ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
    public static extern int StartDocPrinter(IntPtr hPrinter, int level, [In, MarshalAs(UnmanagedType.LPStruct)] DOCINFOA di);

    [DllImport("winspool.Drv", EntryPoint = "EndDocPrinter", SetLastError = true, ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
    public static extern bool EndDocPrinter(IntPtr hPrinter);

    [DllImport("winspool.Drv", EntryPoint = "StartPagePrinter", SetLastError = true, ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
    public static extern bool StartPagePrinter(IntPtr hPrinter);

    [DllImport("winspool.Drv", EntryPoint = "EndPagePrinter", SetLastError = true, ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
    public static extern bool EndPagePrinter(IntPtr hPrinter);

    [DllImport("winspool.Drv", EntryPoint = "WritePrinter", SetLastError = true, ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
    public static extern bool WritePrinter(IntPtr hPrinter, IntPtr pBytes, int dwCount, out int dwWritten);

    public static bool SendBytesToPrinter(string szPrinterName, byte[] bytes)
    {
        IntPtr pUnmanagedBytes = Marshal.AllocCoTaskMem(bytes.Length);
        Marshal.Copy(bytes, 0, pUnmanagedBytes, bytes.Length);

        bool success = false;
        if (OpenPrinter(szPrinterName, out IntPtr hPrinter, IntPtr.Zero))
        {
            DOCINFOA di = new DOCINFOA
            {
                pDocName = "PDV Local Ticket",
                pDataType = "RAW"
            };

            if (StartDocPrinter(hPrinter, 1, di) != 0)
            {
                if (StartPagePrinter(hPrinter))
                {
                    success = WritePrinter(hPrinter, pUnmanagedBytes, bytes.Length, out int dwWritten);
                    EndPagePrinter(hPrinter);
                }
                EndDocPrinter(hPrinter);
            }
            ClosePrinter(hPrinter);
        }
        Marshal.FreeCoTaskMem(pUnmanagedBytes);
        return success;
    }
}
