using MediatR;
using Microsoft.EntityFrameworkCore;
using PDV.Application.Common.Interfaces;
using PDV.Application.Features.Products.Dtos;

namespace PDV.Application.Features.Products.Queries.GetProducts;

public record GetProductsQuery : IRequest<List<ProductDto>>;

public class GetProductsQueryHandler : IRequestHandler<GetProductsQuery, List<ProductDto>>
{
    private readonly IApplicationDbContext _context;

    public GetProductsQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<ProductDto>> Handle(GetProductsQuery request, CancellationToken cancellationToken)
    {
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
                Stock = x.Stock,
                Category = x.Category,
                SaleType = x.SaleType.ToString(),
                Barcode = x.Barcode,
                Cost = x.Cost,
                MinStock = x.MinStock,
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
