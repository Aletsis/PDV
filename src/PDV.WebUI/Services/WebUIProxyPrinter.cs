using Microsoft.JSInterop;
using PDV.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;
using System.Text;
using PDV.Infrastructure.Printing;

namespace PDV.WebUI.Services;

public class WebUIProxyPrinter : IEscPosPrinter
{
    private readonly IJSRuntime _jsRuntime;
    private readonly IApplicationDbContext _context;

    public WebUIProxyPrinter(IJSRuntime jsRuntime, IApplicationDbContext context)
    {
        _jsRuntime = jsRuntime;
        _context = context;
    }

    public async Task<bool> CheckStatusAsync(string ipAddress, int port, CancellationToken cancellationToken = default)
    {
        var targetUri = ipAddress;
        if (!ipAddress.Contains("://"))
        {
            var targetPort = port <= 0 ? 9100 : port;
            targetUri = $"tcp://{ipAddress}:{targetPort}";
        }

        try
        {
            return await _jsRuntime.InvokeAsync<bool>("posCheckPrinterStatus", cancellationToken, targetUri);
        }
        catch
        {
            return false;
        }
    }

    public async Task PrintJobAsync(
        string ipAddress,
        int port,
        string text,
        bool autoCut = true,
        bool partialCut = true,
        bool openDrawerBefore = false,
        bool openDrawerAfter = false,
        int copies = 1,
        int? encodingCodePage = null,
        CancellationToken cancellationToken = default)
    {
        var targetUri = ipAddress;
        if (!ipAddress.Contains("://"))
        {
            var targetPort = port <= 0 ? 9100 : port;
            targetUri = $"tcp://{ipAddress}:{targetPort}";
        }

        string base64Data = string.Empty;
        try
        {
            string searchVal = ipAddress;
            if (ipAddress.StartsWith("usb://", StringComparison.OrdinalIgnoreCase))
            {
                searchVal = ipAddress.Substring(6);
            }
            else if (ipAddress.StartsWith("serial://", StringComparison.OrdinalIgnoreCase))
            {
                int questionMarkIdx = ipAddress.IndexOf('?');
                searchVal = questionMarkIdx > 0 
                    ? ipAddress.Substring(9, questionMarkIdx - 9) 
                    : ipAddress.Substring(9);
            }

            var printer = await _context.Printers
                .FirstOrDefaultAsync(p => p.IpAddress == searchVal || p.DevicePath == searchVal, cancellationToken);
            int width = printer != null && printer.MaxWidth > 0 ? printer.MaxWidth / 12 : 42;
            var encoding = encodingCodePage.HasValue ? Encoding.GetEncoding(encodingCodePage.Value) : Encoding.GetEncoding(1252);
            var bytes = EscPosParser.Parse(text, width, encoding);
            base64Data = Convert.ToBase64String(bytes);
        }
        catch
        {
            // Fallback a base64 simple en caso de error
            base64Data = Convert.ToBase64String(Encoding.GetEncoding(1252).GetBytes(text));
        }

        var job = new
        {
            target = targetUri,
            profile = 1, // EscPos
            contentType = 2, // RawBase64
            data = base64Data,
            codePage = encodingCodePage ?? 1252,
            autoCut = autoCut,
            partialCut = partialCut,
            openDrawerBefore = openDrawerBefore,
            openDrawerAfter = openDrawerAfter,
            copies = Math.Clamp(copies, 1, 5),
            maxRetries = 3,
            timeoutMs = 5000
        };

        try
        {
            await _jsRuntime.InvokeAsync<bool>("posPrintJob", cancellationToken, job);
        }
        catch
        {
            // Fallback a print text
            await PrintTextAsync(ipAddress, port, text, encodingCodePage, cancellationToken);
        }
    }

    public async Task<List<string>> GetInstalledPrintersAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var list = await _jsRuntime.InvokeAsync<string[]>("posGetInstalledPrinters", cancellationToken);
            return list?.ToList() ?? new List<string>();
        }
        catch
        {
            return new List<string>();
        }
    }

    public async Task<List<string>> GetSerialPortsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var list = await _jsRuntime.InvokeAsync<string[]>("posGetSerialPorts", cancellationToken);
            return list?.ToList() ?? new List<string>();
        }
        catch
        {
            return new List<string>();
        }
    }

    public async Task PrintTextAsync(string ipAddress, int port, string text, int? encodingCodePage = null, CancellationToken cancellationToken = default)
    {
        try
        {
            string searchVal = ipAddress;
            if (ipAddress.StartsWith("usb://", StringComparison.OrdinalIgnoreCase))
            {
                searchVal = ipAddress.Substring(6);
            }
            else if (ipAddress.StartsWith("serial://", StringComparison.OrdinalIgnoreCase))
            {
                int questionMarkIdx = ipAddress.IndexOf('?');
                searchVal = questionMarkIdx > 0 
                    ? ipAddress.Substring(9, questionMarkIdx - 9) 
                    : ipAddress.Substring(9);
            }

            var printer = await _context.Printers
                .FirstOrDefaultAsync(p => p.IpAddress == searchVal || p.DevicePath == searchVal, cancellationToken);
            int width = printer != null && printer.MaxWidth > 0 ? printer.MaxWidth / 12 : 42;
            var encoding = encodingCodePage.HasValue ? Encoding.GetEncoding(encodingCodePage.Value) : Encoding.GetEncoding(1252);
            var textBytes = EscPosParser.Parse(text, width, encoding);

            var sb = new List<byte>();
            sb.AddRange(new byte[] { 0x1B, 0x40 }); // Init
            sb.AddRange(textBytes);
            sb.AddRange(new byte[] { 0x0A }); // LF
            sb.AddRange(new byte[] { 0x1B, 0x64, 0x04 }); // Feed 4 lines
            sb.AddRange(new byte[] { 0x1D, 0x56, 0x00 }); // Full cut

            await PrintRawAsync(ipAddress, port, sb.ToArray(), cancellationToken);
        }
        catch
        {
            // Fallback a print text normal en JS en caso de excepción catastrófica
            await _jsRuntime.InvokeVoidAsync("posPrintText", cancellationToken, ipAddress, port, text, encodingCodePage);
        }
    }

    public async Task PrintRawAsync(string ipAddress, int port, byte[] data, CancellationToken cancellationToken = default)
    {
        var base64Data = Convert.ToBase64String(data);
        await _jsRuntime.InvokeVoidAsync("posPrintRaw", cancellationToken, ipAddress, port, base64Data);
    }

    public async Task PrintImageAsync(string ipAddress, int port, byte[] imagePngBytes, int maxWidth = 384, CancellationToken cancellationToken = default)
    {
        var base64Image = Convert.ToBase64String(imagePngBytes);
        await _jsRuntime.InvokeVoidAsync("posPrintImage", cancellationToken, ipAddress, port, base64Image, maxWidth);
    }

    public async Task PrintBarcodeAsync(string ipAddress, int port, string data, int barcodeType = 73, int height = 100, CancellationToken cancellationToken = default)
    {
        await _jsRuntime.InvokeVoidAsync("posPrintBarcode", cancellationToken, ipAddress, port, data, barcodeType, height);
    }

    public async Task PrintQrAsync(string ipAddress, int port, string data, int moduleSize = 4, int errorLevel = 48, CancellationToken cancellationToken = default)
    {
        await _jsRuntime.InvokeVoidAsync("posPrintQr", cancellationToken, ipAddress, port, data, moduleSize, errorLevel);
    }

    public async Task OpenDrawerAsync(string ipAddress, int port, CancellationToken cancellationToken = default)
    {
        await _jsRuntime.InvokeVoidAsync("posOpenDrawer", cancellationToken, ipAddress, port);
    }
}
