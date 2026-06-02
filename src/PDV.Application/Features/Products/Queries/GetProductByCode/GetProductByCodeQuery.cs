using MediatR;
using Microsoft.EntityFrameworkCore;
using PDV.Application.Common.Interfaces;
using PDV.Domain.Entities;

namespace PDV.Application.Features.Products.Queries.GetProductByCode;

public record GetProductByCodeQuery(string Code) : IRequest<Product?>;

public class GetProductByCodeQueryHandler : IRequestHandler<GetProductByCodeQuery, Product?>
{
    private readonly IApplicationDbContext _context;

    public GetProductByCodeQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Product?> Handle(GetProductByCodeQuery request, CancellationToken cancellationToken)
    {
        return await _context.Products
            .FirstOrDefaultAsync(x => 
                (x.Code == request.Code || x.Plu == request.Code) && x.IsActive, 
                cancellationToken);
    }
}
