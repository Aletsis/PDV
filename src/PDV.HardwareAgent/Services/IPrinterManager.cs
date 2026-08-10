using PDV.HardwareAgent.Contracts.Models;

namespace PDV.HardwareAgent.Services;

public interface IPrinterManager
{
    Task<PrintResult> PrintJobAsync(PrintJobRequest request, CancellationToken ct = default);
    Task<PrinterStatusResult> CheckStatusAsync(string targetEndpoint, CancellationToken ct = default);
    Task<LocalDevicesResult> GetLocalDevicesAsync();
}
