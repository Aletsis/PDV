using MediatR;
using Microsoft.EntityFrameworkCore;
using PDV.Application.Common.Interfaces;
using PDV.Application.Features.Products.Dtos;

namespace PDV.Application.Features.Products.Queries.GetProducts;

public record GetProductsQuery(Guid? BranchId = null) : IRequest<List<ProductDto>>;

public class GetProductsQueryHandler : IRequestHandler<GetProductsQuery, List<ProductDto>>
{
    private readonly IApplicationDbContext _context;

    public GetProductsQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<ProductDto>> Handle(GetProductsQuery request, CancellationToken cancellationToken)
    {
        var filterByBranch = request.BranchId.HasValue && request.BranchId.Value != Guid.Empty;
        var targetBranchId = request.BranchId ?? Guid.Empty;

        return await _context.Products
            .Select(x => new ProductDto
            {
                Id = x.Id,
                Name = x.Name,
                Code = x.Code,
                Plu = x.Plu,
                Description = x.Description,
                Price = x.Price,
                WholesalePrice = x.WholesalePrice,
                WholesaleMinQuantity = x.WholesaleMinQuantity,
                Stock = filterByBranch
                    ? _context.ProductBranchStocks
                        .Where(s => s.ProductId == x.Id && s.BranchId == targetBranchId)
                        .Select(s => s.Stock)
                        .FirstOrDefault()
                    : _context.ProductBranchStocks
                        .Where(s => s.ProductId == x.Id)
                        .Sum(s => s.Stock),
                Category = x.Category,
                SaleType = x.SaleType.ToString(),
                Barcode = x.Barcode,
                Cost = x.Cost,
                MinStock = filterByBranch
                    ? _context.ProductBranchStocks
                        .Where(s => s.ProductId == x.Id && s.BranchId == targetBranchId)
                        .Select(s => s.MinStock)
                        .FirstOrDefault()
                    : _context.ProductBranchStocks
                        .Where(s => s.ProductId == x.Id)
                        .Select(s => s.MinStock)
                        .FirstOrDefault(),
                TaxRate = x.TaxRate.ToString(),
                IsActive = x.IsActive,
                SatCode = x.SatCode,
                Type = (int)x.Type,
                ControlExistencia = (int)x.ControlExistencia,
                SaleUnitId = x.SaleUnitId,
                SaleUnitName = x.SaleUnitName,
                XmlUnitId = x.XmlUnitId,
                Department = x.Department,
                Clasificacion1Id = x.Clasificacion1Id,
                Clasificacion5Id = x.Clasificacion5Id
            })
            .ToListAsync(cancellationToken);
    }
}
