namespace PDV.Application.Features.Orders.Dtos;

public class OrderItemDto
{
    public Guid Id { get; set; }
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public decimal UnitPrice { get; set; }
    public decimal? PriceOverride { get; set; }
    public decimal Quantity { get; set; }
    public decimal RequestedQuantity { get; set; }
    public decimal TotalPrice { get; set; }
    public string? Notes { get; set; }
    public bool IsFulfilled { get; set; }
    public bool IsReturned { get; set; }
}