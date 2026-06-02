namespace PDV.Application.Features.Sales.Dtos;

public class InvoiceDto
{
    public Guid Id { get; set; }
    public string InvoiceNumber { get; set; } = string.Empty;
    public DateTime InvoiceDate { get; set; }
    public Guid? SaleId { get; set; }
    public string SaleNumber { get; set; } = string.Empty;
    public Guid? ClientId { get; set; }
    public string ClientName { get; set; } = string.Empty;
    public decimal Subtotal { get; set; }
    public decimal Tax { get; set; }
    public decimal Total { get; set; }
    public bool IsGlobal { get; set; }

    public string? Uuid { get; set; }
    public string? SelloDigitalEmisor { get; set; }
    public string? SelloDigitalSAT { get; set; }
    public string? NoCertificadoEmisor { get; set; }
    public string? NoCertificadoSAT { get; set; }
    public string? CadenaOriginal { get; set; }
    public DateTime? StampedAt { get; set; }
}
