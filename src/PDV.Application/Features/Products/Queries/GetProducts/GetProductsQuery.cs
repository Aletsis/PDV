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
        var branchId = request.BranchId;
        if (branchId == null || branchId == Guid.Empty)
        {
            var activeShift = await _context.Shifts
                .Include(s => s.CashRegister)
                .FirstOrDefaultAsync(s => s.Status == PDV.Domain.Enums.ShiftStatus.Open, cancellationToken);
            branchId = activeShift?.CashRegister?.BranchId;

            if (branchId == null || branchId == Guid.Empty)
            {
                var firstBranch = await _context.Branches.FirstOrDefaultAsync(cancellationToken);
                branchId = firstBranch?.Id;
            }
        }

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
                Stock = _context.ProductBranchStocks
                    .Where(s => s.ProductId == x.Id && s.BranchId == branchId)
                    .Select(s => s.Stock)
                    .FirstOrDefault(),
                Category = x.Category,
                SaleType = x.SaleType.ToString(),
                Barcode = x.Barcode,
                Cost = x.Cost,
                MinStock = _context.ProductBranchStocks
                    .Where(s => s.ProductId == x.Id && s.BranchId == branchId)
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
