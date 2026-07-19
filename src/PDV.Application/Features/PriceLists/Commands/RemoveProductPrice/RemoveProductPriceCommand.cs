using MediatR;
using PDV.Application.Common.Interfaces;
using PDV.Domain.Common.Models;
using Microsoft.EntityFrameworkCore;

namespace PDV.Application.Features.PriceLists.Commands.RemoveProductPrice;

public record RemoveProductPriceCommand(Guid PriceListId, Guid ProductId) : IRequest<Result>;

public class RemoveProductPriceCommandHandler : IRequestHandler<RemoveProductPriceCommand, Result>
{
    private readonly IApplicationDbContext _context;

    public RemoveProductPriceCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result> Handle(RemoveProductPriceCommand request, CancellationToken cancellationToken)
    {
        var priceList = await _context.PriceLists
            .Include(pl => pl.ProductPrices)
            .FirstOrDefaultAsync(pl => pl.Id == request.PriceListId, cancellationToken);

        if (priceList == null)
        {
            return Result.Failure(Error.NotFound("PriceList.NotFound", $"No se encontró la lista de precios con ID '{request.PriceListId}'."));
        }

        try
        {
            priceList.RemovePrice(request.ProductId);
            await _context.SaveChangesAsync(cancellationToken);
            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Failure(Error.Failure("PriceList.RemovePriceFailed", ex.Message));
        }
    }
}
