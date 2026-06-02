using Microsoft.AspNetCore.Mvc;
using PDV.Application.Common.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;
using PDV.Application.Features.Logos.Queries.GetLogo;
using PDV.Application.Features.Printers.Queries.GetPrinter;

namespace PDV.WebUI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PrintController : ControllerBase
{
    private readonly IEscPosPrinter _printer;
    private readonly IMediator _mediator;
    private readonly ILogger<PrintController> _logger;

    public PrintController(IEscPosPrinter printer, IMediator mediator, ILogger<PrintController> logger)
    {
        _printer = printer;
        _mediator = mediator;
        _logger = logger;
    }

    [HttpPost("tcp")] // POST /api/print/tcp
    public async Task<IActionResult> PrintTcp([FromBody] PrintRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Ip)) return BadRequest("Ip required");
        if (req.Port <= 0) req.Port = 9100; // default common port

        try
        {
            _logger.LogInformation("PrintTcp called to {Ip}:{Port}", req.Ip, req.Port);
            await _printer.PrintTextAsync(req.Ip, req.Port, req.Text ?? string.Empty);
            _logger.LogInformation("PrintTcp to {Ip}:{Port} completed", req.Ip, req.Port);
            return Accepted();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in PrintTcp to {Ip}:{Port}", req.Ip, req.Port);
            return Problem(detail: ex.Message);
        }
    }

    [HttpPost("image")] // POST /api/print/image
    public async Task<IActionResult> PrintImage([FromBody] PrintImageRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Ip)) return BadRequest("Ip required");
        if (req.Port <= 0) req.Port = 9100;
        if (req.ImageBase64 == null) return BadRequest("ImageBase64 required");

        try
        {
            _logger.LogInformation("PrintImage called to {Ip}:{Port} size={MaxWidth}", req.Ip, req.Port, req.MaxWidth);
            var bytes = Convert.FromBase64String(req.ImageBase64);
            await _printer.PrintImageAsync(req.Ip, req.Port, bytes, req.MaxWidth);
            _logger.LogInformation("PrintImage to {Ip}:{Port} completed", req.Ip, req.Port);
            return Accepted();
        }
        catch (FormatException fex)
        {
            _logger.LogWarning(fex, "Invalid Base64 in PrintImage request for {Ip}:{Port}", req.Ip, req.Port);
            return BadRequest("ImageBase64 is not valid Base64");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error printing image to {Ip}:{Port}", req.Ip, req.Port);
            return Problem(detail: ex.Message);
        }
    }

    [HttpPost("logo/{printerId}/{logoId}")]
    public async Task<IActionResult> PrintLogoFromDb(Guid printerId, Guid logoId, CancellationToken cancellationToken)
    {
        _logger.LogInformation("PrintLogoFromDb called for printerId={PrinterId} logoId={LogoId}", printerId, logoId);

        var printerDto = await _mediator.Send(new GetPrinterQuery(printerId));
        if (printerDto == null)
        {
            _logger.LogWarning("Printer {PrinterId} not found", printerId);
            return NotFound($"Printer {printerId} not found");
        }

        var logoDto = await _mediator.Send(new GetLogoQuery(logoId));
        if (logoDto == null)
        {
            _logger.LogWarning("Logo {LogoId} not found", logoId);
            return NotFound($"Logo {logoId} not found");
        }

        byte[] imageBytes;
        try
        {
            imageBytes = Convert.FromBase64String(logoDto.Base64);
        }
        catch (FormatException fex)
        {
            _logger.LogWarning(fex, "Logo {LogoId} has invalid Base64", logoId);
            return BadRequest("Logo data is not valid base64");
        }

        try
        {
            var maxWidth = printerDto.MaxWidth > 0 ? printerDto.MaxWidth : 384;
            _logger.LogInformation("Printing logo {LogoId} to {Ip}:{Port} (maxWidth={MaxWidth})", logoId, printerDto.IpAddress, printerDto.Port, maxWidth);
            await _printer.PrintImageAsync(printerDto.IpAddress ?? string.Empty, printerDto.Port ?? 9100, imageBytes, maxWidth, cancellationToken);
            _logger.LogInformation("Printed logo {LogoId} to printer {PrinterId}", logoId, printerId);
            return Accepted();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error printing logo {LogoId} to printer {PrinterId}", logoId, printerId);
            return Problem(detail: ex.Message);
        }
    }

    [HttpPost("barcode")] // POST /api/print/barcode
    public async Task<IActionResult> PrintBarcode([FromBody] PrintBarcodeRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Ip)) return BadRequest("Ip required");
        if (req.Port <= 0) req.Port = 9100;
        if (string.IsNullOrWhiteSpace(req.Data)) return BadRequest("Data required");

        try
        {
            await _printer.PrintBarcodeAsync(req.Ip, req.Port, req.Data, req.BarcodeType, req.Height);
            return Accepted();
        }
        catch (Exception ex)
        {
            return Problem(detail: ex.Message);
        }
    }

    [HttpPost("qr")] // POST /api/print/qr
    public async Task<IActionResult> PrintQr([FromBody] PrintQrRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Ip)) return BadRequest("Ip required");
        if (req.Port <= 0) req.Port = 9100;
        if (string.IsNullOrWhiteSpace(req.Data)) return BadRequest("Data required");

        try
        {
            await _printer.PrintQrAsync(req.Ip, req.Port, req.Data, req.ModuleSize, req.ErrorLevel);
            return Accepted();
        }
        catch (Exception ex)
        {
            return Problem(detail: ex.Message);
        }
    }

    public class PrintRequest
    {
        public string? Ip { get; set; }
        public int Port { get; set; } = 9100;
        public string? Text { get; set; }
    }

    public class PrintImageRequest
    {
        public string? Ip { get; set; }
        public int Port { get; set; } = 9100;
        public string? ImageBase64 { get; set; }
        public int MaxWidth { get; set; } = 384;
    }

    public class PrintBarcodeRequest
    {
        public string? Ip { get; set; }
        public int Port { get; set; } = 9100;
        public string? Data { get; set; }
        public int BarcodeType { get; set; } = 73; // CODE128 fallback
        public int Height { get; set; } = 100;
    }

    public class PrintQrRequest
    {
        public string? Ip { get; set; }
        public int Port { get; set; } = 9100;
        public string? Data { get; set; }
        public int ModuleSize { get; set; } = 4;
        public int ErrorLevel { get; set; } = 48; // 48..51 -> L..H
    }
}
