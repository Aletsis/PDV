namespace PDV.Application.Features.Printers.Dtos;

public class PrinterDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? IpAddress { get; set; }
    public int? Port { get; set; }
    public int CodePage { get; set; }
    public int MaxWidth { get; set; }
    public bool IsActive { get; set; }
}
