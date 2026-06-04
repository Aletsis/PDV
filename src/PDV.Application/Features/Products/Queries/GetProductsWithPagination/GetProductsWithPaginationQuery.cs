using MediatR;
using Microsoft.EntityFrameworkCore;
using PDV.Application.Common.Interfaces;
using PDV.Application.Common.Models;
using PDV.Application.Features.Products.Dtos;
using PDV.Domain.Enums;

namespace PDV.Application.Features.Products.Queries.GetProductsWithPagination;

public record GetProductsWithPaginationQuery : IRequest<PaginatedList<ProductDto>>
{
    public int PageNumber { get; init; } = 1;
    public int PageSize { get; init; } = 10;
    public string? SearchQuery { get; init; }
    public ProductType? Type { get; init; }
}

public class GetProductsWithPaginationQueryHandler : IRequestHandler<GetProductsWithPaginationQuery, PaginatedList<ProductDto>>
{
    private readonly IApplicationDbContext _context;

    public GetProductsWithPaginationQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<PaginatedList<ProductDto>> Handle(GetProductsWithPaginationQuery request, CancellationToken cancellationToken)
    {
        var query = _context.Products.AsNoTracking();

        // Solo buscar/cargar productos activos
        query = query.Where(x => x.IsActive);

        if (request.Type.HasValue)
        {
            query = query.Where(x => x.Type == request.Type.Value);
        }

        if (!string.IsNullOrWhiteSpace(request.SearchQuery))
        {
            var search = request.SearchQuery.Trim().ToLower();
            query = query.Where(x =>
                x.Name.ToLower().Contains(search) ||
                x.Code.ToLower().Contains(search) ||
                (x.Category != null && x.Category.ToLower().Contains(search)) ||
                (x.Plu != null && x.Plu.ToLower().Contains(search))
            );
        }

        // Proyección a DTO para optimizar la consulta SQL
        var projection = query.Select(x => new ProductDto
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
        });

        return await PaginatedList<ProductDto>.CreateAsync(projection, request.PageNumber, request.PageSize, cancellationToken);
    }
}
