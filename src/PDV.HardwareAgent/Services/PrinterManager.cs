#pragma warning disable CA1416

using System.Diagnostics;
using System.Drawing.Printing;
using System.IO.Ports;
using PDV.HardwareAgent.Contracts.Enums;
using PDV.HardwareAgent.Contracts.Models;
using PDV.HardwareAgent.Profiles;
using PDV.HardwareAgent.Transports;

namespace PDV.HardwareAgent.Services;

public class PrinterManager : IPrinterManager
{
    private readonly ITransportFactory _transportFactory;
    private readonly IPrinterProfileFactory _profileFactory;
    private readonly ILogger<PrinterManager> _logger;

    public PrinterManager(
        ITransportFactory transportFactory,
        IPrinterProfileFactory profileFactory,
        ILogger<PrinterManager> logger)
    {
        _transportFactory = transportFactory;
        _profileFactory = profileFactory;
        _logger = logger;
    }

    public async Task<PrintResult> PrintJobAsync(PrintJobRequest request, CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();

        // 1. Validar la solicitud del documento
        var validationError = ValidateRequest(request);
        if (validationError != null)
        {
            _logger.LogWarning("Validación de trabajo de impresión rechazada: {Reason}", validationError);
            return PrintResult.Fail("VALIDATION_ERROR", validationError, 0, sw.ElapsedMilliseconds);
        }

        // 2. Obtener el perfil y el transporte
        IPrinterProfile profile;
        IPrinterTransport transport;
        try
        {
            profile = _profileFactory.GetProfile(request.Profile);
            transport = _transportFactory.CreateTransport(request.Target, request.TimeoutMs);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al instanciar perfil/transporte para '{Target}'", request.Target);
            return PrintResult.Fail("INITIALIZATION_ERROR", ex.Message, 0, sw.ElapsedMilliseconds);
        }

        // 3. Generar la secuencia binaria de comandos
        byte[] payloadBytes;
        try
        {
            payloadBytes = BuildJobPayload(request, profile);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al generar comandos para '{Target}'", request.Target);
            return PrintResult.Fail("COMMAND_GENERATION_ERROR", ex.Message, 0, sw.ElapsedMilliseconds);
        }

        // 4. Ejecutar con política de reintentos y tolerancia a fallos
        int maxRetries = Math.Clamp(request.MaxRetries, 1, 5);
        int attempts = 0;
        Exception? lastException = null;

        while (attempts < maxRetries)
        {
            attempts++;
            ct.ThrowIfCancellationRequested();

            try
            {
                _logger.LogInformation("Enviando trabajo a '{Target}' (Intento {Attempt}/{MaxRetries}, {Bytes} bytes)...",
                    request.Target, attempts, maxRetries, payloadBytes.Length);

                await transport.SendBytesAsync(payloadBytes, ct).ConfigureAwait(false);

                sw.Stop();
                _logger.LogInformation("Trabajo de impresión completado exitosamente en '{Target}' ({Elapsed}ms)",
                    request.Target, sw.ElapsedMilliseconds);

                return PrintResult.Ok(attempts, sw.ElapsedMilliseconds);
            }
            catch (Exception ex)
            {
                lastException = ex;
                _logger.LogWarning(ex, "Fallo en intento {Attempt}/{MaxRetries} al imprimir en '{Target}': {Message}",
                    attempts, maxRetries, request.Target, ex.Message);

                if (attempts < maxRetries)
                {
                    // Backoff lineal/exponencial (150ms, 300ms, 600ms)
                    int delayMs = 150 * (int)Math.Pow(2, attempts - 1);
                    await Task.Delay(delayMs, ct).ConfigureAwait(false);
                }
            }
        }

        sw.Stop();
        var errorMsg = lastException?.Message ?? "Error desconocido al transmitir datos a la impresora.";
        _logger.LogError(lastException, "Trabajo de impresión fallido tras {Attempts} intentos en '{Target}'", attempts, request.Target);

        return PrintResult.Fail("TRANSMISSION_ERROR", errorMsg, attempts, sw.ElapsedMilliseconds);
    }

    public async Task<PrinterStatusResult> CheckStatusAsync(string targetEndpoint, CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            using var transport = _transportFactory.CreateTransport(targetEndpoint, 3000);
            var isOnline = await transport.CheckAvailabilityAsync(ct).ConfigureAwait(false);
            sw.Stop();

            return new PrinterStatusResult
            {
                Target = targetEndpoint,
                IsOnline = isOnline,
                Status = isOnline ? "Online" : "Offline / Unreachable",
                Details = isOnline ? "Dispositivo responde satisfactoriamente." : "No se pudo establecer conexión o el puerto está ocupado.",
                ResponseTimeMs = sw.ElapsedMilliseconds
            };
        }
        catch (Exception ex)
        {
            sw.Stop();
            return new PrinterStatusResult
            {
                Target = targetEndpoint,
                IsOnline = false,
                Status = "Error",
                Details = ex.Message,
                ResponseTimeMs = sw.ElapsedMilliseconds
            };
        }
    }

    public Task<LocalDevicesResult> GetLocalDevicesAsync()
    {
        var result = new LocalDevicesResult();

        // 1. Puertos Seriales
        try
        {
            result.SerialPorts = SerialPort.GetPortNames().Distinct().OrderBy(p => p).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "No se pudieron listar los puertos seriales");
        }

        // 2. Impresoras Instaladas en el Sistema
        try
        {
            foreach (string printer in PrinterSettings.InstalledPrinters)
            {
                result.InstalledPrinters.Add(printer);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "No se pudieron listar las impresoras locales instaladas");
        }

        return Task.FromResult(result);
    }

    private static string? ValidateRequest(PrintJobRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Target))
            return "El destino ('Target') de la impresora es obligatorio.";

        if (string.IsNullOrWhiteSpace(request.Data) && !request.OpenDrawerBefore && !request.OpenDrawerAfter)
            return "El documento a imprimir ('Data') no puede estar vacío.";

        if (request.Copies < 1 || request.Copies > 5)
            return "El número de copias debe estar entre 1 y 5.";

        return null;
    }

    private static byte[] BuildJobPayload(PrintJobRequest request, IPrinterProfile profile)
    {
        var buffer = new List<byte>();

        // 1. Apertura de cajón previo
        if (request.OpenDrawerBefore)
        {
            buffer.AddRange(profile.OpenCashDrawer(0));
        }

        // 2. Inicialización
        buffer.AddRange(profile.Initialize());

        // 3. Contenido según tipo
        byte[] contentBytes = request.ContentType switch
        {
            PrintJobContentType.Text => profile.FormatText(request.Data, request.CodePage),
            PrintJobContentType.RawBase64 => Convert.FromBase64String(request.Data),
            PrintJobContentType.ImageBase64 => profile.ConvertRasterImage(Convert.FromBase64String(request.Data), request.MaxWidth),
            PrintJobContentType.Barcode => profile.PrintBarcode(request.Data, request.BarcodeType, request.BarcodeHeight),
            PrintJobContentType.QrCode => profile.PrintQr(request.Data, request.QrModuleSize, request.QrErrorLevel),
            _ => profile.FormatText(request.Data, request.CodePage)
        };

        // Multiplicar por número de copias
        int copies = Math.Clamp(request.Copies, 1, 5);
        for (int i = 0; i < copies; i++)
        {
            buffer.AddRange(contentBytes);

            // Corte de papel por copia
            if (request.AutoCut)
            {
                buffer.AddRange(profile.CutPaper(request.PartialCut));
            }
        }

        // 4. Apertura de cajón posterior
        if (request.OpenDrawerAfter)
        {
            buffer.AddRange(profile.OpenCashDrawer(0));
        }

        return buffer.ToArray();
    }
}
