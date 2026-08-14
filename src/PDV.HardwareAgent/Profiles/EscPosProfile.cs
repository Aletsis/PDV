#pragma warning disable CA1416

using System.Drawing;
using System.Text;
using PDV.HardwareAgent.Contracts.Enums;

namespace PDV.HardwareAgent.Profiles;

public class EscPosProfile : IPrinterProfile
{
    static EscPosProfile()
    {
        try
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        }
        catch { }
    }

    public PrinterProfileType ProfileType => PrinterProfileType.EscPos;

    public virtual byte[] Initialize() => new byte[] { 0x1B, 0x40 }; // ESC @

    public virtual byte[] CutPaper(bool partialCut = true)
    {
        var list = new List<byte>
        {
            0x1B, 0x64, 0x04, // ESC d 4 (Feed 4 lines)
            0x1D, 0x56, (byte)(partialCut ? 0x01 : 0x00) // GS V 1/0
        };
        return list.ToArray();
    }

    public virtual byte[] OpenCashDrawer(int pin = 0)
    {
        // ESC p m t1 t2 (m: pin 0=drawer1, 1=drawer2, t1=25, t2=250)
        byte m = (byte)(pin == 1 ? 1 : 0);
        return new byte[] { 0x1B, 0x70, m, 0x19, 0xFA };
    }

    public virtual byte[] FormatText(string text, int codePage = 1252)
    {
        var list = new List<byte>();
        Encoding encoding;
        try
        {
            encoding = Encoding.GetEncoding(codePage);
        }
        catch
        {
            try
            {
                encoding = Encoding.Latin1;
            }
            catch
            {
                encoding = Encoding.UTF8;
            }
        }

        list.AddRange(encoding.GetBytes(text));
        return list.ToArray();
    }

    public virtual byte[] PrintBarcode(string data, int barcodeType = 73, int height = 100)
    {
        var cmd = new List<byte>
        {
            0x1D, 0x48, 0x02, // GS H 2 (HRI below)
            0x1D, 0x68, (byte)Math.Clamp(height, 1, 255), // GS h
            0x1D, 0x6B, (byte)barcodeType // GS k
        };
        cmd.AddRange(Encoding.ASCII.GetBytes(data));
        cmd.Add(0x00); // NUL terminator
        cmd.Add(0x0A); // LF
        return cmd.ToArray();
    }

    public virtual byte[] PrintQr(string data, int moduleSize = 4, int errorLevel = 48)
    {
        var list = new List<byte>
        {
            0x1D, 0x28, 0x6B, 0x04, 0x00, 0x31, 0x41, 0x32, 0x00, // Model 2
            0x1D, 0x28, 0x6B, 0x03, 0x00, 0x31, 0x43, (byte)Math.Clamp(moduleSize, 1, 16), // Size
            0x1D, 0x28, 0x6B, 0x03, 0x00, 0x31, 0x45, (byte)Math.Clamp(errorLevel, 48, 51) // EC Level (48=L, 49=M, 50=Q, 51=H)
        };

        var bytes = Encoding.UTF8.GetBytes(data);
        int pL = (bytes.Length + 3) % 256;
        int pH = (bytes.Length + 3) / 256;

        list.AddRange(new byte[] { 0x1D, 0x28, 0x6B, (byte)pL, (byte)pH, 0x31, 0x50, 0x30 }); // Store data
        list.AddRange(bytes);
        list.AddRange(new byte[] { 0x1D, 0x28, 0x6B, 0x03, 0x00, 0x31, 0x51, 0x30 }); // Print QR
        list.Add(0x0A);
        return list.ToArray();
    }

    public virtual byte[] ConvertRasterImage(byte[] imagePngBytes, int maxWidth = 384)
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
        int bytesPerLine = (width + 7) / 8;
        var rasterData = new byte[bytesPerLine * height];

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                var color = resized.GetPixel(x, y);
                int luminance = (int)(0.299 * color.R + 0.587 * color.G + 0.114 * color.B);
                if (luminance < 127) // Umbral negro
                {
                    int index = y * bytesPerLine + x / 8;
                    rasterData[index] |= (byte)(0x80 >> (x % 8));
                }
            }
        }

        var header = new List<byte>();
        int xL = width % 256;
        int xH = width / 256;
        int yL = height % 256;
        int yH = height / 256;

        header.AddRange(new byte[] { 0x1D, 0x76, 0x30, 0x00, (byte)xL, (byte)xH, (byte)yL, (byte)yH }); // GS v 0
        header.AddRange(rasterData);
        header.Add(0x0A);

        return header.ToArray();
    }
}
