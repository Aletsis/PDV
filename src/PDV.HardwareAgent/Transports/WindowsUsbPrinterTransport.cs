using PDV.Domain.Enums;

namespace PDV.HardwareAgent.Transports;

public class WindowsUsbPrinterTransport : IPrinterTransport
{
    private readonly string _printerQueueName;

    public PrinterConnectionType ConnectionType => PrinterConnectionType.Usb;
    public string TargetEndpoint => $"usb://{_printerQueueName}";

    public WindowsUsbPrinterTransport(string printerQueueName)
    {
        if (string.IsNullOrWhiteSpace(printerQueueName))
            throw new ArgumentException("El nombre de la cola de impresión USB es requerido.", nameof(printerQueueName));

        _printerQueueName = printerQueueName.Trim();
    }

    public Task<bool> CheckAvailabilityAsync(CancellationToken ct = default)
    {
        var exists = RawPrinterHelper.PrinterExists(_printerQueueName);
        return Task.FromResult(exists);
    }

    public Task SendBytesAsync(byte[] data, CancellationToken ct = default)
    {
        return Task.Run(() =>
        {
            var success = RawPrinterHelper.SendBytesToPrinter(_printerQueueName, data);
            if (!success)
            {
                throw new IOException($"No se pudo escribir en la impresora USB de Windows: '{_printerQueueName}'. Verifique conexión y papel.");
            }
        }, ct);
    }

    public void Dispose() { }
}
