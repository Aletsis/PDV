using Microsoft.AspNetCore.Mvc;
using PDV.Application.Common.Interfaces;
using PDV.HardwareAgent.Contracts.Requests;

namespace PDV.HardwareAgent.Endpoints;

public static class PrinterEndpoints
{
    public static IEndpointRouteBuilder MapPrinterEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api");

        // Health check endpoint for the PWA to verify the agent is running
        app.MapGet("/health", () => Results.Ok(new
        {
            Status = "Healthy",
            Agent = "PDV Hardware Agent",
            Version = "1.0.0",
            Machine = Environment.MachineName,
            Timestamp = DateTime.UtcNow
        }));

        // Endpoint: Print formatted plain text
        group.MapPost("/print/text", async (
            [FromBody] PrintTextRequest request,
            [FromServices] IEscPosPrinter printer,
            CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(request.Ip)) return Results.BadRequest("Ip is required.");
            var port = request.Port <= 0 ? 9100 : request.Port;

            try
            {
                await printer.PrintTextAsync(request.Ip, port, request.Text ?? string.Empty, request.EncodingCodePage, cancellationToken);
                return Results.Accepted();
            }
            catch (Exception ex)
            {
                return Results.Problem(detail: ex.Message, title: "Printing failed");
            }
        });

        // Endpoint: Print raw bytes (Base64)
        group.MapPost("/print/raw", async (
            [FromBody] PrintRawRequest request,
            [FromServices] IEscPosPrinter printer,
            CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(request.Ip)) return Results.BadRequest("Ip is required.");
            if (string.IsNullOrWhiteSpace(request.DataBase64)) return Results.BadRequest("DataBase64 is required.");
            var port = request.Port <= 0 ? 9100 : request.Port;

            try
            {
                var rawBytes = Convert.FromBase64String(request.DataBase64);
                await printer.PrintRawAsync(request.Ip, port, rawBytes, cancellationToken);
                return Results.Accepted();
            }
            catch (FormatException)
            {
                return Results.BadRequest("DataBase64 contains invalid Base64 format.");
            }
            catch (Exception ex)
            {
                return Results.Problem(detail: ex.Message, title: "Printing failed");
            }
        });

        // Endpoint: Print image (PNG/JPG Base64)
        group.MapPost("/print/image", async (
            [FromBody] PrintImageRequest request,
            [FromServices] IEscPosPrinter printer,
            CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(request.Ip)) return Results.BadRequest("Ip is required.");
            if (string.IsNullOrWhiteSpace(request.ImageBase64)) return Results.BadRequest("ImageBase64 is required.");
            var port = request.Port <= 0 ? 9100 : request.Port;
            var maxWidth = request.MaxWidth <= 0 ? 384 : request.MaxWidth;

            try
            {
                var bytes = Convert.FromBase64String(request.ImageBase64);
                await printer.PrintImageAsync(request.Ip, port, bytes, maxWidth, cancellationToken);
                return Results.Accepted();
            }
            catch (FormatException)
            {
                return Results.BadRequest("ImageBase64 contains invalid Base64 format.");
            }
            catch (Exception ex)
            {
                return Results.Problem(detail: ex.Message, title: "Printing failed");
            }
        });

        // Endpoint: Print Barcode
        group.MapPost("/print/barcode", async (
            [FromBody] PrintBarcodeRequest request,
            [FromServices] IEscPosPrinter printer,
            CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(request.Ip)) return Results.BadRequest("Ip is required.");
            if (string.IsNullOrWhiteSpace(request.Data)) return Results.BadRequest("Data is required.");
            var port = request.Port <= 0 ? 9100 : request.Port;
            var barcodeType = request.BarcodeType <= 0 ? 73 : request.BarcodeType;
            var height = request.Height <= 0 ? 100 : request.Height;

            try
            {
                await printer.PrintBarcodeAsync(request.Ip, port, request.Data, barcodeType, height, cancellationToken);
                return Results.Accepted();
            }
            catch (Exception ex)
            {
                return Results.Problem(detail: ex.Message, title: "Printing failed");
            }
        });

        // Endpoint: Print QR Code
        group.MapPost("/print/qr", async (
            [FromBody] PrintQrRequest request,
            [FromServices] IEscPosPrinter printer,
            CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(request.Ip)) return Results.BadRequest("Ip is required.");
            if (string.IsNullOrWhiteSpace(request.Data)) return Results.BadRequest("Data is required.");
            var port = request.Port <= 0 ? 9100 : request.Port;
            var moduleSize = request.ModuleSize <= 0 ? 4 : request.ModuleSize;
            var errorLevel = request.ErrorLevel <= 0 ? 48 : request.ErrorLevel;

            try
            {
                await printer.PrintQrAsync(request.Ip, port, request.Data, moduleSize, errorLevel, cancellationToken);
                return Results.Accepted();
            }
            catch (Exception ex)
            {
                return Results.Problem(detail: ex.Message, title: "Printing failed");
            }
        });

        // Endpoint: Open Cash Drawer
        group.MapPost("/drawer/open", async (
            [FromBody] DrawerRequest request,
            [FromServices] IEscPosPrinter printer,
            CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(request.Ip)) return Results.BadRequest("Ip is required.");
            var port = request.Port <= 0 ? 9100 : request.Port;

            try
            {
                await printer.OpenDrawerAsync(request.Ip, port, cancellationToken);
                return Results.Accepted();
            }
            catch (Exception ex)
            {
                return Results.Problem(detail: ex.Message, title: "Opening cash drawer failed");
            }
        });

        return app;
    }
}
