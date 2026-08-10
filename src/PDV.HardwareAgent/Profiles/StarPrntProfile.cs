using System.Drawing;
using System.Text;
using PDV.HardwareAgent.Contracts.Enums;

namespace PDV.HardwareAgent.Profiles;

public class StarPrntProfile : IPrinterProfile
{
    static StarPrntProfile()
    {
        try
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        }
        catch { }
    }

    public PrinterProfileType ProfileType => PrinterProfileType.StarPrnt;

    public byte[] Initialize() => new byte[] { 0x1B, 0x3F, 0x0A, 0x00 }; // Reset printer buffer

    public byte[] CutPaper(bool partialCut = true)
    {
        return new byte[]
        {
            0x1B, 0x64, (byte)(partialCut ? 0x02 : 0x03) // ESC d 2 (partial) or ESC d 3 (full)
        };
    }

    public byte[] OpenCashDrawer(int pin = 0)
    {
        // Star Micronics uses BEL (0x07) for pulse 1, SUB (0x1A) for pulse 2
        return pin == 1 ? new byte[] { 0x1A } : new byte[] { 0x07 };
    }

    public byte[] FormatText(string text, int codePage = 1252)
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

    public byte[] PrintBarcode(string data, int barcodeType = 73, int height = 100)
    {
        // Star Line Mode Barcode ESC b ...
        var cmd = new List<byte>
        {
            0x1B, 0x62, 0x06, 0x02, (byte)Math.Clamp(height / 8, 1, 24) // ESC b CODE128
        };
        cmd.AddRange(Encoding.ASCII.GetBytes(data));
        cmd.Add(0x1E); // Record separator
        cmd.Add(0x0A);
        return cmd.ToArray();
    }

    public byte[] PrintQr(string data, int moduleSize = 4, int errorLevel = 48)
    {
        // Fallback ESC/POS standard QR or Star 2D barcode
        var list = new List<byte>
        {
            0x1D, 0x28, 0x6B, 0x04, 0x00, 0x31, 0x41, 0x32, 0x00,
            0x1D, 0x28, 0x6B, 0x03, 0x00, 0x31, 0x43, (byte)Math.Clamp(moduleSize, 1, 16),
            0x1D, 0x28, 0x6B, 0x03, 0x00, 0x31, 0x45, (byte)Math.Clamp(errorLevel, 48, 51)
        };

        var bytes = Encoding.UTF8.GetBytes(data);
        int pL = (bytes.Length + 3) % 256;
        int pH = (bytes.Length + 3) / 256;

        list.AddRange(new byte[] { 0x1D, 0x28, 0x6B, (byte)pL, (byte)pH, 0x31, 0x50, 0x30 });
        list.AddRange(bytes);
        list.AddRange(new byte[] { 0x1D, 0x28, 0x6B, 0x03, 0x00, 0x31, 0x51, 0x30 });
        list.Add(0x0A);
        return list.ToArray();
    }

    public byte[] ConvertRasterImage(byte[] imagePngBytes, int maxWidth = 384)
    {
        // Use standard raster graphics compatible with Star graphic mode
        var escPos = new EscPosProfile();
        return escPos.ConvertRasterImage(imagePngBytes, maxWidth);
    }
}
