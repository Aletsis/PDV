using FluentValidation;
using MediatR;
using PDV.Application.Common.Interfaces;
using PDV.Domain.Common.Models;
using Microsoft.EntityFrameworkCore;

namespace PDV.Application.Features.PriceLists.Commands.UpdatePriceList;

public record UpdatePriceListCommand : IRequest<Result>
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
}

public class UpdatePriceListCommandValidator : AbstractValidator<UpdatePriceListCommand>
{
    public UpdatePriceListCommandValidator()
    {
        RuleFor(v => v.Id)
            .NotEmpty().WithMessage("El ID de la lista de precios es requerido.");

        RuleFor(v => v.Name)
            .NotEmpty().WithMessage("El nombre de la lista de precios es requerido.")
            .MaximumLength(100).WithMessage("El nombre no puede superar los 100 caracteres.");

        RuleFor(v => v.Description)
            .MaximumLength(250).WithMessage("La descripción no puede superar los 250 caracteres.");
    }
}

public class UpdatePriceListCommandHandler : IRequestHandler<UpdatePriceListCommand, Result>
{
    private readonly IApplicationDbContext _context;
    private readonly IValidator<UpdatePriceListCommand> _validator;

    public UpdatePriceListCommandHandler(IApplicationDbContext context, IValidator<UpdatePriceListCommand> validator)
    {
        _context = context;
        _validator = validator;
    }

    public async Task<Result> Handle(UpdatePriceListCommand request, CancellationToken cancellationToken)
    {
        var validationResult = await _validator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            return Result.Failure(Error.Validation("PriceList.Validation", validationResult.Errors.First().ErrorMessage));
        }

        var priceList = await _context.PriceLists.FirstOrDefaultAsync(pl => pl.Id == request.Id, cancellationToken);
        if (priceList == null)
        {
            return Result.Failure(Error.NotFound("PriceList.NotFound", $"No se encontró la lista de precios con ID '{request.Id}'."));
        }

        // Verificar duplicados por nombre
        var exists = await _context.PriceLists.AnyAsync(pl => pl.Name == request.Name.Trim() && pl.Id != request.Id, cancellationToken);
        if (exists)
        {
            return Result.Failure(Error.Conflict("PriceList.DuplicateName", $"Ya existe otra lista de precios con el nombre '{request.Name}'."));
        }

        try
        {
            priceList.Update(request.Name, request.Description);
            await _context.SaveChangesAsync(cancellationToken);

            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Failure(Error.Failure("PriceList.UpdateFailed", ex.Message));
        }
    }
}
