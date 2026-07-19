using FluentValidation;
using MediatR;
using PDV.Application.Common.Interfaces;
using PDV.Domain.Entities;
using PDV.Domain.Common.Models;
using Microsoft.EntityFrameworkCore;

namespace PDV.Application.Features.PriceLists.Commands.CreatePriceList;

public record CreatePriceListCommand : IRequest<Result<Guid>>
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
}

public class CreatePriceListCommandValidator : AbstractValidator<CreatePriceListCommand>
{
    public CreatePriceListCommandValidator()
    {
        RuleFor(v => v.Name)
            .NotEmpty().WithMessage("El nombre de la lista de precios es requerido.")
            .MaximumLength(100).WithMessage("El nombre no puede superar los 100 caracteres.");

        RuleFor(v => v.Description)
            .MaximumLength(250).WithMessage("La descripción no puede superar los 250 caracteres.");
    }
}

public class CreatePriceListCommandHandler : IRequestHandler<CreatePriceListCommand, Result<Guid>>
{
    private readonly IApplicationDbContext _context;
    private readonly IValidator<CreatePriceListCommand> _validator;

    public CreatePriceListCommandHandler(IApplicationDbContext context, IValidator<CreatePriceListCommand> validator)
    {
        _context = context;
        _validator = validator;
    }

    public async Task<Result<Guid>> Handle(CreatePriceListCommand request, CancellationToken cancellationToken)
    {
        var validationResult = await _validator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            return Result.Failure<Guid>(Error.Validation("PriceList.Validation", validationResult.Errors.First().ErrorMessage));
        }

        // Verificar duplicados por nombre
        var exists = await _context.PriceLists.AnyAsync(pl => pl.Name == request.Name.Trim(), cancellationToken);
        if (exists)
        {
            return Result.Failure<Guid>(Error.Conflict("PriceList.DuplicateName", $"Ya existe una lista de precios con el nombre '{request.Name}'."));
        }

        try
        {
            var priceList = new PriceList(request.Name, request.Description);
            _context.PriceLists.Add(priceList);
            await _context.SaveChangesAsync(cancellationToken);

            return Result.Success(priceList.Id);
        }
        catch (Exception ex)
        {
            return Result.Failure<Guid>(Error.Failure("PriceList.CreateFailed", ex.Message));
        }
    }
}
