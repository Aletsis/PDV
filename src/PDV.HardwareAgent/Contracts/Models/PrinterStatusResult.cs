namespace PDV.HardwareAgent.Contracts.Models;

public class PrinterStatusResult
{
    public string Target { get; set; } = string.Empty;
    public bool IsOnline { get; set; }
    public string Status { get; set; } = "Unknown";
    public string? Details { get; set; }
    public long ResponseTimeMs { get; set; }
}

public class LocalDevicesResult
{
    public List<string> SerialPorts { get; set; } = new();
    public List<string> InstalledPrinters { get; set; } = new();
    public string MachineName { get; set; } = Environment.MachineName;
    public string OperatingSystem { get; set; } = Environment.OSVersion.ToString();
}
