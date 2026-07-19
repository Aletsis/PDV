using MediatR;
using PDV.Application.Features.PriceLists.Dtos;
using PDV.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace PDV.Application.Features.PriceLists.Queries.GetPriceLists;

public record GetPriceListsQuery(bool IncludeInactive = false) : IRequest<List<PriceListDto>>;

public class GetPriceListsQueryHandler : IRequestHandler<GetPriceListsQuery, List<PriceListDto>>
{
    private readonly IApplicationDbContext _context;

    public GetPriceListsQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<PriceListDto>> Handle(GetPriceListsQuery request, CancellationToken cancellationToken)
    {
        var query = _context.PriceLists.AsNoTracking();

        if (!request.IncludeInactive)
        {
            query = query.Where(pl => pl.IsActive);
        }

        var list = await query
            .OrderBy(pl => pl.Name)
            .ToListAsync(cancellationToken);

        return list.Select(pl => new PriceListDto(
            pl.Id,
            pl.Name,
            pl.Description,
            pl.IsActive
        )).ToList();
    }
}
