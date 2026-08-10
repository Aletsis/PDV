using PDV.Domain.Enums;

namespace PDV.HardwareAgent.Transports;

public interface IPrinterTransport : IDisposable
{
    PrinterConnectionType ConnectionType { get; }
    string TargetEndpoint { get; }
    Task<bool> CheckAvailabilityAsync(CancellationToken ct = default);
    Task SendBytesAsync(byte[] data, CancellationToken ct = default);
}
