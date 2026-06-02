using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PDV.Application.Common.Interfaces;

namespace PDV.Application.Features.Products.Queries.GetProductsDelta;

public record GetProductsDeltaQuery(DateTime SinceUtc) : IRequest<List<ProductSyncDto>>;

public class ProductSyncDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string? Plu { get; set; }
    public string? Barcode { get; set; }
    public string? Description { get; set; }
    public decimal Price { get; set; }
    public decimal? WholesalePrice { get; set; }
    public decimal? WholesaleMinQuantity { get; set; }
    public decimal Cost { get; set; }
    public decimal Stock { get; set; }
    public decimal MinStock { get; set; }
    public string Category { get; set; } = string.Empty;
    public string SaleType { get; set; } = "Piece";
    public string TaxRate { get; set; } = "Rate16";
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? LastModifiedAt { get; set; }

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

public class GetProductsDeltaQueryHandler : IRequestHandler<GetProductsDeltaQuery, List<ProductSyncDto>>
{
    private readonly IApplicationDbContext _context;

    public GetProductsDeltaQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<ProductSyncDto>> Handle(GetProductsDeltaQuery request, CancellationToken cancellationToken)
    {
        var since = request.SinceUtc;

        return await _context.Products
            .IgnoreQueryFilters() // In case soft-deleted or inactive items need to be sync'd too
            .Where(p => p.CreatedAt > since || (p.LastModifiedAt != null && p.LastModifiedAt > since))
            .Select(p => new ProductSyncDto
            {
                Id = p.Id,
                Name = p.Name,
                Code = p.Code,
                Plu = p.Plu,
                Barcode = p.Barcode,
                Description = p.Description,
                Price = p.Price,
                WholesalePrice = p.WholesalePrice,
                WholesaleMinQuantity = p.WholesaleMinQuantity,
                Cost = p.Cost,
                Stock = p.Stock,
                MinStock = p.MinStock,
                Category = p.Category,
                SaleType = p.SaleType.ToString(),
                TaxRate = p.TaxRate.ToString(),
                IsActive = p.IsActive,
                CreatedAt = p.CreatedAt,
                LastModifiedAt = p.LastModifiedAt,
                SatCode = p.SatCode,
                Type = (int)p.Type,
                ControlExistencia = (int)p.ControlExistencia,
                SaleUnitId = p.SaleUnitId,
                SaleUnitName = p.SaleUnitName,
                XmlUnitId = p.XmlUnitId,
                Department = p.Department,
                Clasificacion1Id = p.Clasificacion1Id,
                Clasificacion5Id = p.Clasificacion5Id
            })
            .ToListAsync(cancellationToken);
    }
}
