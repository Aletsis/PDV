#pragma warning disable CA1416

using System.Net.Sockets;
using System.Text;
using System.Drawing;
using System.Drawing.Imaging;
using PDV.Application.Common.Interfaces;

namespace PDV.Infrastructure.Printing;

// Simple TCP-based ESC/POS printer sender. Sends init, text and cut commands.
public class TcpEscPosPrinter : IEscPosPrinter
{
    private readonly Encoding _defaultEncoding = Encoding.GetEncoding(1252);

    private Encoding ChooseEncoding(string text, int? codePage)
    {
        if (codePage.HasValue)
        {
            try { return Encoding.GetEncoding(codePage.Value); } catch { }
        }
        // If text contains non-ascii chars, prefer Windows-1252 for Latin accents
        if (text.Any(c => c > 127)) return _defaultEncoding;
        return Encoding.ASCII;
    }

    // Convert a PNG/JPEG byte array to ESC/POS raster format and print
    public async Task PrintImageAsync(string ipAddress, int port, byte[] imagePngBytes, int maxWidth = 384, CancellationToken cancellationToken = default)
    {
        using var ms = new MemoryStream(imagePngBytes);
        using var bmp = new Bitmap(ms);

        // Resize to maxWidth maintaining aspect
        var width = bmp.Width;
        var height = bmp.Height;
        if (width > maxWidth)
        {
            var ratio = (double)maxWidth / width;
            width = maxWidth;
            height = (int)(height * ratio);
        }

        using var resized = new Bitmap(bmp, new Size(width, height));

        // Convert to monochrome bitmap bytes (raster)
        var imgBytes = ConvertBitmapToRaster(resized);

        var header = new List<byte>();
        header.AddRange(new byte[] { 0x1B, 0x40 }); // init

        // Select bit-image mode - GS v 0
        // xL xH yL yH
        int xL = width % 256;
        int xH = width / 256;
        int yL = height % 256;
        int yH = height / 256;

        header.AddRange(new byte[] { 0x1D, 0x76, 0x30, 0x00, (byte)xL, (byte)xH, (byte)yL, (byte)yH });
        header.AddRange(imgBytes);

        // feed and cut
        header.AddRange(new byte[] { 0x0A, 0x1B, 0x64, 0x02, 0x1D, 0x56, 0x00 });

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
                // luminance
                int luminance = (int)(0.299 * color.R + 0.587 * color.G + 0.114 * color.B);
                bool black = luminance < 127;
                if (black)
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
        cmd.AddRange(new byte[] { 0x1B, 0x40 }); // init
        // Set HRI position (below)
        cmd.AddRange(new byte[] { 0x1D, 0x48, 0x02 });
        // Set barcode height
        cmd.AddRange(new byte[] { 0x1D, 0x68, (byte)height });
        // Print barcode (GS k)
        cmd.AddRange(new byte[] { 0x1D, 0x6B, (byte)barcodeType });
        cmd.AddRange(Encoding.ASCII.GetBytes(data));
        cmd.Add(0x00);
        cmd.AddRange(new byte[] { 0x0A, 0x1D, 0x56, 0x00 });

        await PrintRawAsync(ipAddress, port, cmd.ToArray(), cancellationToken);
    }

    public async Task PrintQrAsync(string ipAddress, int port, string data, int moduleSize = 4, int errorLevel = 48, CancellationToken cancellationToken = default)
    {
        // ESC/POS QR code sequence (Store, Set size, Set error, Print)
        var list = new List<byte>();
        list.AddRange(new byte[] { 0x1B, 0x40 }); // init

        // Select model 2
        list.AddRange(new byte[] { 0x1D, 0x28, 0x6B, 0x04, 0x00, 0x31, 0x41, 0x32, 0x00 });
        // Set module size
        list.AddRange(new byte[] { 0x1D, 0x28, 0x6B, 0x03, 0x00, 0x31, 0x43, (byte)moduleSize });
        // Set error correction
        list.AddRange(new byte[] { 0x1D, 0x28, 0x6B, 0x03, 0x00, 0x31, 0x45, (byte)errorLevel });

        // Store data
        var bytes = Encoding.UTF8.GetBytes(data);
        int pL = (bytes.Length + 3) % 256;
        int pH = (bytes.Length + 3) / 256;
        list.AddRange(new byte[] { 0x1D, 0x28, 0x6B, (byte)pL, (byte)pH, 0x31, 0x50, 0x30 });
        list.AddRange(bytes);

        // Print
        list.AddRange(new byte[] { 0x1D, 0x28, 0x6B, 0x03, 0x00, 0x31, 0x51, 0x30 });

        // feed and cut
        list.AddRange(new byte[] { 0x0A, 0x1B, 0x64, 0x02, 0x1D, 0x56, 0x00 });

        await PrintRawAsync(ipAddress, port, list.ToArray(), cancellationToken);
    }

    public async Task PrintTextAsync(string ipAddress, int port, string text, int? encodingCodePage = null, CancellationToken cancellationToken = default)
    {
        var sb = new List<byte>();

        // Initialize printer
        sb.AddRange(new byte[] { 0x1B, 0x40 });

        // Determine encoding
        var encoding = ChooseEncoding(text, encodingCodePage);
        sb.AddRange(encoding.GetBytes(text));
        sb.AddRange(new byte[] { 0x0A }); // LF

        // Feed and cut
        sb.AddRange(new byte[] { 0x1B, 0x64, 0x03 }); // Feed n lines
        sb.AddRange(new byte[] { 0x1D, 0x56, 0x00 }); // Full cut

        await PrintRawAsync(ipAddress, port, sb.ToArray(), cancellationToken);
    }

    public async Task PrintRawAsync(string ipAddress, int port, byte[] data, CancellationToken cancellationToken = default)
    {
        using var client = new TcpClient();
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        await client.ConnectAsync(ipAddress, port, cts.Token).ConfigureAwait(false);
        using var stream = client.GetStream();
        await stream.WriteAsync(data, 0, data.Length, cts.Token).ConfigureAwait(false);
        await stream.FlushAsync(cts.Token).ConfigureAwait(false);
        // Give printer a moment
        await Task.Delay(100, cts.Token).ConfigureAwait(false);
    }

    public async Task OpenDrawerAsync(string ipAddress, int port, CancellationToken cancellationToken = default)
    {
        // ESC p m t1 t2 (27 112 0 25 250)
        var cmd = new byte[] { 0x1B, 0x70, 0x00, 0x19, 0xFA };
        await PrintRawAsync(ipAddress, port, cmd, cancellationToken);
    }
}
