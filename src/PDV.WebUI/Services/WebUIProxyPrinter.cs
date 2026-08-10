using Microsoft.JSInterop;
using PDV.Application.Common.Interfaces;

namespace PDV.WebUI.Services;

public class WebUIProxyPrinter : IEscPosPrinter
{
    private readonly IJSRuntime _jsRuntime;

    public WebUIProxyPrinter(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
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

        var job = new
        {
            target = targetUri,
            profile = 1, // EscPos
            contentType = 1, // Text
            data = text,
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
        await _jsRuntime.InvokeVoidAsync("posPrintText", cancellationToken, ipAddress, port, text, encodingCodePage);
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
