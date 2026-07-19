using System;
using System.Threading;
using System.Threading.Tasks;
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
