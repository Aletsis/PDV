using FluentValidation;
using MediatR;
using PDV.Application.Common.Interfaces;
using PDV.Domain.ValueObjects;
using PDV.Domain.Common.Models;
using Microsoft.EntityFrameworkCore;

namespace PDV.Application.Features.Companies.Commands.UpdateCompany;

public record UpdateCompanyCommand : IRequest<Result>
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string RFC { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string? Email { get; set; }
    
    // Address fields
    public string Street { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public string ZipCode { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
}

public class UpdateCompanyCommandValidator : AbstractValidator<UpdateCompanyCommand>
{
    public UpdateCompanyCommandValidator()
    {
        RuleFor(v => v.Id)
            .NotEmpty().WithMessage("El ID de la empresa es requerido.");

        RuleFor(v => v.Name)
            .NotEmpty().WithMessage("El nombre de la empresa es requerido.")
            .MaximumLength(150).WithMessage("El nombre no puede superar los 150 caracteres.");

        RuleFor(v => v.RFC)
            .NotEmpty().WithMessage("El RFC es requerido.")
            .Length(12, 13).WithMessage("El RFC debe tener entre 12 y 13 caracteres.")
            .Matches(@"^[A-Z&Ññ]{3,4}[0-9]{6}[A-Z0-9]{3}$").WithMessage("El formato del RFC es inválido.");

        RuleFor(v => v.Phone)
            .MaximumLength(20).WithMessage("El teléfono no puede superar los 20 caracteres.");

        RuleFor(v => v.Email)
            .EmailAddress().WithMessage("El formato de correo electrónico es inválido.")
            .When(x => !string.IsNullOrWhiteSpace(x.Email));
    }
}

public class UpdateCompanyCommandHandler : IRequestHandler<UpdateCompanyCommand, Result>
{
    private readonly IApplicationDbContext _context;
    private readonly IValidator<UpdateCompanyCommand> _validator;

    public UpdateCompanyCommandHandler(IApplicationDbContext context, IValidator<UpdateCompanyCommand> validator)
    {
        _context = context;
        _validator = validator;
    }

    public async Task<Result> Handle(UpdateCompanyCommand request, CancellationToken cancellationToken)
    {
        var validationResult = await _validator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            return Result.Failure(Error.Validation("Company.Validation", validationResult.Errors.First().ErrorMessage));
        }

        var company = await _context.Companies.FirstOrDefaultAsync(c => c.Id == request.Id, cancellationToken);
        if (company == null)
        {
            return Result.Failure(Error.NotFound("Company.NotFound", $"No se encontró la empresa con ID '{request.Id}'."));
        }

        // Verificar si existe otra empresa con ese RFC
        var duplicateRFC = await _context.Companies.AnyAsync(c => c.RFC == request.RFC.Trim().ToUpperInvariant() && c.Id != request.Id, cancellationToken);
        if (duplicateRFC)
        {
            return Result.Failure(Error.Conflict("Company.DuplicateRFC", $"Ya existe otra empresa registrada con el RFC '{request.RFC}'."));
        }

        Address? address = null;
        if (!string.IsNullOrWhiteSpace(request.Street))
        {
            address = Address.Create(
                request.Street,
                request.City,
                request.State,
                request.ZipCode,
                request.Country);
        }

        try
        {
            company.Update(
                request.Name,
                request.RFC,
                address,
                request.Phone,
                request.Email
            );

            await _context.SaveChangesAsync(cancellationToken);
            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Failure(Error.Failure("Company.UpdateFailed", ex.Message));
        }
    }
}
