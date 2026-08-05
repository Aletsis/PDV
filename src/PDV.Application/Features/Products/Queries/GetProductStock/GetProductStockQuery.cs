using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PDV.Application.Common.Interfaces;

namespace PDV.Application.Features.Products.Queries.GetProductStock;

public record GetProductStockQuery(Guid ProductId, Guid BranchId) : IRequest<decimal>;

public class GetProductStockQueryHandler : IRequestHandler<GetProductStockQuery, decimal>
{
    private readonly IApplicationDbContext _context;

    public GetProductStockQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<decimal> Handle(GetProductStockQuery request, CancellationToken cancellationToken)
    {
        var stock = await _context.ProductBranchStocks
            .Where(x => x.ProductId == request.ProductId && x.BranchId == request.BranchId)
            .Select(x => x.Stock)
            .FirstOrDefaultAsync(cancellationToken);

        return stock;
    }
}
