using MediatR;
using PDV.Application.Common.Interfaces;
using PDV.Domain.Common.Models;
using Microsoft.EntityFrameworkCore;

namespace PDV.Application.Features.PriceLists.Commands.DeletePriceList;

public record DeletePriceListCommand(Guid Id) : IRequest<Result>;

public class DeletePriceListCommandHandler : IRequestHandler<DeletePriceListCommand, Result>
{
    private readonly IApplicationDbContext _context;

    public DeletePriceListCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result> Handle(DeletePriceListCommand request, CancellationToken cancellationToken)
    {
        var priceList = await _context.PriceLists.FirstOrDefaultAsync(pl => pl.Id == request.Id, cancellationToken);
        if (priceList == null)
        {
            return Result.Failure(Error.NotFound("PriceList.NotFound", $"No se encontró la lista de precios con ID '{request.Id}'."));
        }

        try
        {
            _context.PriceLists.Remove(priceList);
            await _context.SaveChangesAsync(cancellationToken);
            
            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Failure(Error.Failure("PriceList.DeleteFailed", ex.Message));
        }
    }
}
