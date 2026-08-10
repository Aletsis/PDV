using PDV.Domain.Enums;

namespace PDV.HardwareAgent.Transports;

public class PosixUsbPrinterTransport : IPrinterTransport
{
    private readonly string _devicePath;

    public PrinterConnectionType ConnectionType => PrinterConnectionType.Usb;
    public string TargetEndpoint => $"usb://{_devicePath}";

    public PosixUsbPrinterTransport(string devicePath)
    {
        if (string.IsNullOrWhiteSpace(devicePath))
            throw new ArgumentException("La ruta del dispositivo USB POSIX es requerida.", nameof(devicePath));

        _devicePath = devicePath.Trim();
    }

    public Task<bool> CheckAvailabilityAsync(CancellationToken ct = default)
    {
        return Task.FromResult(File.Exists(_devicePath));
    }

    public async Task SendBytesAsync(byte[] data, CancellationToken ct = default)
    {
        using var fs = new FileStream(_devicePath, FileMode.Open, FileAccess.Write, FileShare.ReadWrite);
        await fs.WriteAsync(data, 0, data.Length, ct).ConfigureAwait(false);
        await fs.FlushAsync(ct).ConfigureAwait(false);
    }

    public void Dispose() { }
}
