using FluentValidation;
using MediatR;
using PDV.Application.Common.Interfaces;
using PDV.Domain.Common.Models;
using Microsoft.EntityFrameworkCore;

namespace PDV.Application.Features.PriceLists.Commands.AddOrUpdateProductPrice;

public record AddOrUpdateProductPriceCommand(Guid PriceListId, Guid ProductId, decimal Price) : IRequest<Result>;

public class AddOrUpdateProductPriceCommandValidator : AbstractValidator<AddOrUpdateProductPriceCommand>
{
    public AddOrUpdateProductPriceCommandValidator()
    {
        RuleFor(v => v.PriceListId)
            .NotEmpty().WithMessage("El ID de la lista de precios es requerido.");

        RuleFor(v => v.ProductId)
            .NotEmpty().WithMessage("El ID del producto es requerido.");

        RuleFor(v => v.Price)
            .GreaterThanOrEqualTo(0).WithMessage("El precio no puede ser negativo.");
    }
}

public class AddOrUpdateProductPriceCommandHandler : IRequestHandler<AddOrUpdateProductPriceCommand, Result>
{
    private readonly IApplicationDbContext _context;
    private readonly IValidator<AddOrUpdateProductPriceCommand> _validator;

    public AddOrUpdateProductPriceCommandHandler(IApplicationDbContext context, IValidator<AddOrUpdateProductPriceCommand> validator)
    {
        _context = context;
        _validator = validator;
    }

    public async Task<Result> Handle(AddOrUpdateProductPriceCommand request, CancellationToken cancellationToken)
    {
        var validationResult = await _validator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            return Result.Failure(Error.Validation("PriceList.Validation", validationResult.Errors.First().ErrorMessage));
        }

        // Obtener la lista de precios junto con sus relaciones
        var priceList = await _context.PriceLists
            .Include(pl => pl.ProductPrices)
            .FirstOrDefaultAsync(pl => pl.Id == request.PriceListId, cancellationToken);

        if (priceList == null)
        {
            return Result.Failure(Error.NotFound("PriceList.NotFound", $"No se encontró la lista de precios con ID '{request.PriceListId}'."));
        }

        // Verificar que el producto exista
        var productExists = await _context.Products.AnyAsync(p => p.Id == request.ProductId, cancellationToken);
        if (!productExists)
        {
            return Result.Failure(Error.NotFound("Product.NotFound", $"No se encontró el producto con ID '{request.ProductId}'."));
        }

        try
        {
            priceList.AddOrUpdatePrice(request.ProductId, request.Price);
            await _context.SaveChangesAsync(cancellationToken);
            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Failure(Error.Failure("PriceList.UpdatePriceFailed", ex.Message));
        }
    }
}
