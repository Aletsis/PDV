namespace PDV.Application.Features.Sales.Dtos;

public class SaleItemDto
{
    public Guid Id { get; set; }
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public decimal UnitPrice { get; set; }
    public decimal? PriceOverride { get; set; }
    public decimal Quantity { get; set; }
    public decimal TotalPrice { get; set; }
    public bool IsReturned { get; set; }
}
