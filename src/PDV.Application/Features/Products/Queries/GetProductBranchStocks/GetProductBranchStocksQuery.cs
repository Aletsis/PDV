using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PDV.Application.Common.Interfaces;
using PDV.Application.Features.Products.Dtos;

namespace PDV.Application.Features.Products.Queries.GetProductBranchStocks;

public record GetProductBranchStocksQuery(Guid ProductId) : IRequest<List<ProductBranchStockDto>>;

public class GetProductBranchStocksQueryHandler : IRequestHandler<GetProductBranchStocksQuery, List<ProductBranchStockDto>>
{
    private readonly IApplicationDbContext _context;

    public GetProductBranchStocksQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<ProductBranchStockDto>> Handle(GetProductBranchStocksQuery request, CancellationToken cancellationToken)
    {
        var branches = await _context.Branches
            .AsNoTracking()
            .OrderBy(b => b.Name)
            .ToListAsync(cancellationToken);

        var branchStocks = await _context.ProductBranchStocks
            .AsNoTracking()
            .Where(s => s.ProductId == request.ProductId)
            .ToListAsync(cancellationToken);

        var result = new List<ProductBranchStockDto>();

        foreach (var branch in branches)
        {
            var stockInfo = branchStocks.FirstOrDefault(s => s.BranchId == branch.Id);

            result.Add(new ProductBranchStockDto
            {
                BranchId = branch.Id,
                BranchName = branch.Name,
                BranchCode = branch.Code,
                Stock = stockInfo?.Stock ?? 0m,
                MinStock = stockInfo?.MinStock ?? 0m,
                IsActive = branch.IsActive
            });
        }

        return result;
    }
}
