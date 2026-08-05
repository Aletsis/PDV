using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PDV.Application.Common.Interfaces;

namespace PDV.Application.Features.Products.Queries.GetProductBranchStocksDelta;

public record GetProductBranchStocksDeltaQuery(DateTime SinceUtc) : IRequest<List<ProductBranchStockSyncDto>>;

public class ProductBranchStockSyncDto
{
    public Guid Id { get; set; }
    public Guid ProductId { get; set; }
    public Guid BranchId { get; set; }
    public decimal Stock { get; set; }
    public decimal MinStock { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? LastModifiedAt { get; set; }
}

public class GetProductBranchStocksDeltaQueryHandler : IRequestHandler<GetProductBranchStocksDeltaQuery, List<ProductBranchStockSyncDto>>
{
    private readonly IApplicationDbContext _context;

    public GetProductBranchStocksDeltaQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<ProductBranchStockSyncDto>> Handle(GetProductBranchStocksDeltaQuery request, CancellationToken cancellationToken)
    {
        var since = request.SinceUtc;

        return await _context.ProductBranchStocks
            .IgnoreQueryFilters() // In case soft-deleted or inactive items need to be sync'd too
            .Where(s => s.CreatedAt > since || (s.LastModifiedAt != null && s.LastModifiedAt > since))
            .Select(s => new ProductBranchStockSyncDto
            {
                Id = s.Id,
                ProductId = s.ProductId,
                BranchId = s.BranchId,
                Stock = s.Stock,
                MinStock = s.MinStock,
                IsDeleted = s.IsDeleted,
                CreatedAt = s.CreatedAt,
                LastModifiedAt = s.LastModifiedAt
            })
            .ToListAsync(cancellationToken);
    }
}
