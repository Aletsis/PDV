using PDV.Domain.Entities;

namespace PDV.Application.Features.Products.Dtos;

public class ProductDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string? Plu { get; set; }
    public string? Description { get; set; }
    public decimal Price { get; set; }
    public decimal? WholesalePrice { get; set; }
    public decimal? WholesaleMinQuantity { get; set; }
    public decimal Stock { get; set; }
    public string Category { get; set; } = string.Empty;
    public string SaleType { get; set; } = string.Empty;
    public string? Barcode { get; set; }
    public decimal Cost { get; set; }
    public decimal MinStock { get; set; }
    public string TaxRate { get; set; } = string.Empty;
    public bool IsActive { get; set; }

    public string SatCode { get; set; } = string.Empty;
    public int Type { get; set; }
    public int ControlExistencia { get; set; }
    public int? SaleUnitId { get; set; }
    public string SaleUnitName { get; set; } = string.Empty;
    public int? XmlUnitId { get; set; }
    public string Department { get; set; } = string.Empty;
    public int? Clasificacion1Id { get; set; }
    public int? Clasificacion5Id { get; set; }
}
