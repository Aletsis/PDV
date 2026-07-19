using MediatR;
using PDV.Application.Features.PriceLists.Dtos;
using PDV.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;
using PDV.Domain.Common.Models;

namespace PDV.Application.Features.PriceLists.Queries.GetPriceListById;

public record GetCompanyByIdQuery(Guid Id) : IRequest<Result<PriceListDto>>; // Symmetrical

public record GetPriceListByIdQuery(Guid Id) : IRequest<Result<PriceListDto>>;

public class GetPriceListByIdQueryHandler : IRequestHandler<GetPriceListByIdQuery, Result<PriceListDto>>
{
    private readonly IApplicationDbContext _context;

    public GetPriceListByIdQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<PriceListDto>> Handle(GetPriceListByIdQuery request, CancellationToken cancellationToken)
    {
        var priceList = await _context.PriceLists
            .AsNoTracking()
            .FirstOrDefaultAsync(pl => pl.Id == request.Id, cancellationToken);

        if (priceList == null)
        {
            return Result.Failure<PriceListDto>(Error.NotFound("PriceList.NotFound", $"No se encontró la lista de precios con ID '{request.Id}'."));
        }

        var dto = new PriceListDto(
            priceList.Id,
            priceList.Name,
            priceList.Description,
            priceList.IsActive
        );

        return Result.Success(dto);
    }
}
