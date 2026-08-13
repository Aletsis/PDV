#pragma warning disable CA1416

using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace PDV.Infrastructure.Printing;

public static class EscPosParser
{
    public static byte[] Parse(string text, int widthCharacters, Encoding encoding)
    {
        var bytes = new List<byte>();
        var lines = text.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);

        foreach (var line in lines)
        {
            var trimmed = line.Trim();

            // 1. Verificar si es una etiqueta de Logotipo
            if (trimmed.StartsWith("[LOGO:", StringComparison.OrdinalIgnoreCase) && trimmed.EndsWith("]"))
            {
                var base64 = trimmed.Substring(6, trimmed.Length - 7);
                try
                {
                    var logoBytes = Convert.FromBase64String(base64);
                    var rasterBytes = ConvertLogoToRaster(logoBytes, widthCharacters);
                    bytes.AddRange(rasterBytes);
                    bytes.Add(0x0A); // Salto de línea después del logo
                }
                catch
                {
                    // Fallback silencioso si falla la imagen
                }
                continue;
            }
            if (trimmed.Equals("[LOGO]", StringComparison.OrdinalIgnoreCase))
            {
                // No hay imagen cargada
                continue;
            }

            // 2. Verificar si es una etiqueta de Código QR
            if (trimmed.StartsWith("[QR:", StringComparison.OrdinalIgnoreCase) && trimmed.EndsWith("]"))
            {
                var qrData = trimmed.Substring(4, trimmed.Length - 5);
                bytes.AddRange(GetQrCodeBytes(qrData));
                bytes.Add(0x0A);
                continue;
            }

            // 3. Verificar si es una etiqueta de Código de Barras
            if (trimmed.StartsWith("[BARCODE:", StringComparison.OrdinalIgnoreCase) && trimmed.EndsWith("]"))
            {
                var barcodeData = trimmed.Substring(9, trimmed.Length - 10);
                bytes.AddRange(GetBarcodeBytes(barcodeData));
                bytes.Add(0x0A);
                continue;
            }

            // 4. Procesar estilos en línea de texto ordinaria
            bool bold = line.Contains("<B>", StringComparison.OrdinalIgnoreCase);
            string? fontSize = null;

            if (line.Contains("<DH>", StringComparison.OrdinalIgnoreCase))
                fontSize = "doubleheight";
            else if (line.Contains("<DW>", StringComparison.OrdinalIgnoreCase))
                fontSize = "doublewidth";
            else if (line.Contains("<LG>", StringComparison.OrdinalIgnoreCase))
                fontSize = "large";

            // Limpiar las etiquetas XML del texto final que se va a imprimir
            var cleanText = Regex.Replace(line, @"<\/?(B|DH|DW|LG)>", "", RegexOptions.IgnoreCase);

            // Aplicar comandos ESC/POS de estilo
            bytes.AddRange(GetStyleBytes(bold, fontSize));

            // Codificar el texto limpio y escribirlo a los bytes
            bytes.AddRange(encoding.GetBytes(cleanText));
            bytes.Add(0x0A); // LF

            // Limpiar y resetear estilos de la impresora para la siguiente línea
            bytes.AddRange(GetResetStyleBytes());
        }

        return bytes.ToArray();
    }

    private static byte[] GetStyleBytes(bool bold, string? fontSize)
    {
        var list = new List<byte>();

        if (bold)
        {
            list.AddRange(new byte[] { 0x1B, 0x45, 0x01 }); // Bold ON
        }

        if (fontSize != null)
        {
            switch (fontSize)
            {
                case "doubleheight":
                    list.AddRange(new byte[] { 0x1D, 0x21, 0x01 }); // Double Height
                    break;
                case "doublewidth":
                    list.AddRange(new byte[] { 0x1D, 0x21, 0x10 }); // Double Width
                    break;
                case "large":
                    list.AddRange(new byte[] { 0x1D, 0x21, 0x11 }); // Double Width + Height
                    break;
            }
        }

        return list.ToArray();
    }

    private static byte[] GetResetStyleBytes()
    {
        return new byte[] {
            0x1B, 0x45, 0x00, // Bold OFF
            0x1D, 0x21, 0x00  // Size Normal
        };
    }

    private static byte[] GetQrCodeBytes(string data)
    {
        var list = new List<byte>();
        list.AddRange(new byte[] { 0x1D, 0x28, 0x6B, 0x04, 0x00, 0x31, 0x41, 0x32, 0x00 }); // Model 2
        list.AddRange(new byte[] { 0x1D, 0x28, 0x6B, 0x03, 0x00, 0x31, 0x43, 0x04 }); // Module size 4
        list.AddRange(new byte[] { 0x1D, 0x28, 0x6B, 0x03, 0x00, 0x31, 0x45, 0x30 }); // Error correction L

        var bytes = Encoding.UTF8.GetBytes(data);
        int pL = (bytes.Length + 3) % 256;
        int pH = (bytes.Length + 3) / 256;
        list.AddRange(new byte[] { 0x1D, 0x28, 0x6B, (byte)pL, (byte)pH, 0x31, 0x50, 0x30 });
        list.AddRange(bytes);

        list.AddRange(new byte[] { 0x1D, 0x28, 0x6B, 0x03, 0x00, 0x31, 0x51, 0x30 }); // Print QR

        return list.ToArray();
    }

    private static byte[] GetBarcodeBytes(string data)
    {
        var cmd = new List<byte>();
        cmd.AddRange(new byte[] { 0x1D, 0x48, 0x02 }); // HRI below
        cmd.AddRange(new byte[] { 0x1D, 0x68, 100 });  // Height 100
        cmd.AddRange(new byte[] { 0x1D, 0x6B, 73 });   // CODE93
        var bytes = Encoding.ASCII.GetBytes(data);
        cmd.Add((byte)bytes.Length);
        cmd.AddRange(bytes);

        return cmd.ToArray();
    }

    private static byte[] ConvertLogoToRaster(byte[] imagePngBytes, int widthCharacters)
    {
        int maxWidth = widthCharacters >= 42 ? 384 : 256;
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
        var data = new byte[bytesPerLine * height];

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                var color = resized.GetPixel(x, y);
                int luminance = (int)(0.299 * color.R + 0.587 * color.G + 0.114 * color.B);
                if (luminance < 127) // Black threshold
                {
                    int index = y * bytesPerLine + x / 8;
                    data[index] |= (byte)(0x80 >> (x % 8));
                }
            }
        }

        var header = new List<byte>();
        int xL = width % 256;
        int xH = width / 256;
        int yL = height % 256;
        int yH = height / 256;

        header.AddRange(new byte[] { 0x1D, 0x76, 0x30, 0x00, (byte)xL, (byte)xH, (byte)yL, (byte)yH });
        header.AddRange(data);
        return header.ToArray();
    }
}
