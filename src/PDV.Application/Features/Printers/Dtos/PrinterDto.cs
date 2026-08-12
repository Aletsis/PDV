using PDV.Domain.Enums;

namespace PDV.Application.Features.Printers.Dtos;

public class PrinterDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public PrinterConnectionType ConnectionType { get; set; } = PrinterConnectionType.Network;
    public string? IpAddress { get; set; }
    public int? Port { get; set; }
    public string? DevicePath { get; set; }
    public int CodePage { get; set; }
    public int MaxWidth { get; set; }
    public bool IsActive { get; set; }
}
