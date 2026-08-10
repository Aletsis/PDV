using Microsoft.AspNetCore.Mvc;
using PDV.HardwareAgent.Contracts.Enums;
using PDV.HardwareAgent.Contracts.Models;
using PDV.HardwareAgent.Contracts.Requests;
using PDV.HardwareAgent.Services;

namespace PDV.HardwareAgent.Endpoints;

public static class PrinterEndpoints
{
    public static IEndpointRouteBuilder MapPrinterEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api");

        // 1. Health check del agente
        app.MapGet("/health", () => Results.Ok(new
        {
            Status = "Healthy",
            Agent = "PDV Hardware Agent",
            Version = "2.0.0",
            Machine = Environment.MachineName,
            Timestamp = DateTime.UtcNow
        }));

        // 2. Endpoint Unificado: Ejecución de trabajo de impresión estructurado
        group.MapPost("/print/job", async (
            [FromBody] PrintJobRequest request,
            [FromServices] IPrinterManager printerManager,
            CancellationToken cancellationToken) =>
        {
            var result = await printerManager.PrintJobAsync(request, cancellationToken);
            return result.Success ? Results.Ok(result) : Results.BadRequest(result);
        });

        // 3. Endpoint Diagnóstico: Comprobar conectividad y estado de la impresora
        group.MapPost("/printer/status", async (
            [FromBody] PrinterStatusRequest request,
            [FromServices] IPrinterManager printerManager,
            CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(request.Target))
                return Results.BadRequest("Target is required.");

            var status = await printerManager.CheckStatusAsync(request.Target, cancellationToken);
            return Results.Ok(status);
        });

        // 4. Endpoints de Descubrimiento de Dispositivos Locales
        group.MapGet("/devices/all", async ([FromServices] IPrinterManager printerManager) =>
        {
            var devices = await printerManager.GetLocalDevicesAsync();
            return Results.Ok(devices);
        });

        group.MapGet("/devices/ports", async ([FromServices] IPrinterManager printerManager) =>
        {
            var devices = await printerManager.GetLocalDevicesAsync();
            return Results.Ok(devices.SerialPorts);
        });

        group.MapGet("/devices/printers", async ([FromServices] IPrinterManager printerManager) =>
        {
            var devices = await printerManager.GetLocalDevicesAsync();
            return Results.Ok(devices.InstalledPrinters);
        });

        // ──────────────────────────────────────────────────────────────────────────
        // 5. Endpoints de Compatibilidad hacia atrás (PWA / WebUI existente)
        // ──────────────────────────────────────────────────────────────────────────

        group.MapPost("/print/text", async (
            [FromBody] PrintTextRequest request,
            [FromServices] IPrinterManager printerManager,
            CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(request.Ip)) return Results.BadRequest("Ip is required.");

            var job = new PrintJobRequest
            {
                Target = NormalizeEndpoint(request.Ip, request.Port),
                ContentType = PrintJobContentType.Text,
                Data = request.Text ?? string.Empty,
                CodePage = request.EncodingCodePage ?? 1252,
                AutoCut = true,
                PartialCut = true
            };

            var result = await printerManager.PrintJobAsync(job, cancellationToken);
            return result.Success ? Results.Accepted() : Results.Problem(detail: result.ErrorMessage, title: result.ErrorCode);
        });

        group.MapPost("/print/raw", async (
            [FromBody] PrintRawRequest request,
            [FromServices] IPrinterManager printerManager,
            CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(request.Ip)) return Results.BadRequest("Ip is required.");
            if (string.IsNullOrWhiteSpace(request.DataBase64)) return Results.BadRequest("DataBase64 is required.");

            var job = new PrintJobRequest
            {
                Target = NormalizeEndpoint(request.Ip, request.Port),
                ContentType = PrintJobContentType.RawBase64,
                Data = request.DataBase64,
                AutoCut = false // RAW payload typically includes its own cut
            };

            var result = await printerManager.PrintJobAsync(job, cancellationToken);
            return result.Success ? Results.Accepted() : Results.Problem(detail: result.ErrorMessage, title: result.ErrorCode);
        });

        group.MapPost("/print/image", async (
            [FromBody] PrintImageRequest request,
            [FromServices] IPrinterManager printerManager,
            CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(request.Ip)) return Results.BadRequest("Ip is required.");
            if (string.IsNullOrWhiteSpace(request.ImageBase64)) return Results.BadRequest("ImageBase64 is required.");

            var job = new PrintJobRequest
            {
                Target = NormalizeEndpoint(request.Ip, request.Port),
                ContentType = PrintJobContentType.ImageBase64,
                Data = request.ImageBase64,
                MaxWidth = request.MaxWidth <= 0 ? 384 : request.MaxWidth,
                AutoCut = true,
                PartialCut = true
            };

            var result = await printerManager.PrintJobAsync(job, cancellationToken);
            return result.Success ? Results.Accepted() : Results.Problem(detail: result.ErrorMessage, title: result.ErrorCode);
        });

        group.MapPost("/print/barcode", async (
            [FromBody] PrintBarcodeRequest request,
            [FromServices] IPrinterManager printerManager,
            CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(request.Ip)) return Results.BadRequest("Ip is required.");
            if (string.IsNullOrWhiteSpace(request.Data)) return Results.BadRequest("Data is required.");

            var job = new PrintJobRequest
            {
                Target = NormalizeEndpoint(request.Ip, request.Port),
                ContentType = PrintJobContentType.Barcode,
                Data = request.Data,
                BarcodeType = request.BarcodeType <= 0 ? 73 : request.BarcodeType,
                BarcodeHeight = request.Height <= 0 ? 100 : request.Height,
                AutoCut = true,
                PartialCut = true
            };

            var result = await printerManager.PrintJobAsync(job, cancellationToken);
            return result.Success ? Results.Accepted() : Results.Problem(detail: result.ErrorMessage, title: result.ErrorCode);
        });

        group.MapPost("/print/qr", async (
            [FromBody] PrintQrRequest request,
            [FromServices] IPrinterManager printerManager,
            CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(request.Ip)) return Results.BadRequest("Ip is required.");
            if (string.IsNullOrWhiteSpace(request.Data)) return Results.BadRequest("Data is required.");

            var job = new PrintJobRequest
            {
                Target = NormalizeEndpoint(request.Ip, request.Port),
                ContentType = PrintJobContentType.QrCode,
                Data = request.Data,
                QrModuleSize = request.ModuleSize <= 0 ? 4 : request.ModuleSize,
                QrErrorLevel = request.ErrorLevel <= 0 ? 48 : request.ErrorLevel,
                AutoCut = true,
                PartialCut = true
            };

            var result = await printerManager.PrintJobAsync(job, cancellationToken);
            return result.Success ? Results.Accepted() : Results.Problem(detail: result.ErrorMessage, title: result.ErrorCode);
        });

        group.MapPost("/drawer/open", async (
            [FromBody] DrawerRequest request,
            [FromServices] IPrinterManager printerManager,
            CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(request.Ip)) return Results.BadRequest("Ip is required.");

            var job = new PrintJobRequest
            {
                Target = NormalizeEndpoint(request.Ip, request.Port),
                ContentType = PrintJobContentType.Text,
                Data = string.Empty,
                OpenDrawerBefore = true,
                AutoCut = false
            };

            var result = await printerManager.PrintJobAsync(job, cancellationToken);
            return result.Success ? Results.Accepted() : Results.Problem(detail: result.ErrorMessage, title: result.ErrorCode);
        });

        return app;
    }

    private static string NormalizeEndpoint(string ipOrUri, int port)
    {
        if (ipOrUri.Contains("://")) return ipOrUri;
        var p = port <= 0 ? 9100 : port;
        return $"tcp://{ipOrUri}:{p}";
    }
}

public class PrinterStatusRequest
{
    public string Target { get; set; } = string.Empty;
}
