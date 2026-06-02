namespace PDV.Application.Features.Sales.Dtos;

public class SaleDto
{
    public Guid Id { get; set; }
    public string SaleNumber { get; set; } = string.Empty;
    public DateTime Date { get; set; }
    public decimal TotalAmount { get; set; }
    public string PaymentMethod { get; set; } = string.Empty;
    public Guid? ClientId { get; set; }
    public string ClientName { get; set; } = string.Empty;
    public bool IsPaid { get; set; }
    public bool IsCancelled { get; set; }
    public bool IsReturned { get; set; }
    public int ItemCount { get; set; }
}
