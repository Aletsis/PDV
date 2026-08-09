using System;

namespace PDV.Application.Features.Products.Dtos;

public class ProductBranchStockDto
{
    public Guid BranchId { get; set; }
    public string BranchName { get; set; } = string.Empty;
    public string BranchCode { get; set; } = string.Empty;
    public decimal Stock { get; set; }
    public decimal MinStock { get; set; }
    public bool IsLowStock => MinStock > 0 && Stock <= MinStock;
    public bool IsOutOfStock => Stock <= 0;
    public bool IsActive { get; set; } = true;
}
