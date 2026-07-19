using MediatR;
using PDV.Application.Features.PriceLists.Dtos;
using PDV.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace PDV.Application.Features.PriceLists.Queries.GetProductPrices;

public record GetProductPricesQuery(Guid PriceListId, string? SearchString = null) : IRequest<List<PriceListProductDto>>;

public class GetProductPricesQueryHandler : IRequestHandler<GetProductPricesQuery, List<PriceListProductDto>>
{
    private readonly IApplicationDbContext _context;

    public GetProductPricesQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<PriceListProductDto>> Handle(GetProductPricesQuery request, CancellationToken cancellationToken)
    {
        // 1. Obtener todos los productos activos
        var productsQuery = _context.Products.AsNoTracking().Where(p => p.IsActive);

        if (!string.IsNullOrWhiteSpace(request.SearchString))
        {
            var search = request.SearchString.Trim();
            productsQuery = productsQuery.Where(p => 
                EF.Functions.Like(p.Name, $"%{search}%") || 
                EF.Functions.Like(p.Code, $"%{search}%") ||
                (p.Barcode != null && EF.Functions.Like(p.Barcode, $"%{search}%"))
            );
        }

        var products = await productsQuery.OrderBy(p => p.Name).ToListAsync(cancellationToken);

        // 2. Obtener las asignaciones de precios para esta lista
        var customPrices = await _context.PriceListProducts
            .AsNoTracking()
            .Where(pp => pp.PriceListId == request.PriceListId)
            .ToDictionaryAsync(pp => pp.ProductId, pp => pp.Price, cancellationToken);

        // 3. Mapear al DTO
        var list = products.Select(p => {
            var hasCustomPrice = customPrices.TryGetValue(p.Id, out var customPrice);
            return new PriceListProductDto(
                request.PriceListId,
                p.Id,
                p.Code,
                p.Name,
                p.Price, // OriginalPrice
                hasCustomPrice ? customPrice : p.Price, // CustomPrice
                hasCustomPrice // IsOverridden
            );
        }).ToList();

        return list;
    }
}
