using FluentValidation;
using MediatR;
using PDV.Application.Common.Interfaces;
using PDV.Domain.Entities;
using PDV.Domain.ValueObjects;
using PDV.Domain.Common.Models;
using Microsoft.EntityFrameworkCore;

namespace PDV.Application.Features.Companies.Commands.CreateCompany;

public record CreateCompanyCommand : IRequest<Result<Guid>>
{
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

public class CreateCompanyCommandValidator : AbstractValidator<CreateCompanyCommand>
{
    public CreateCompanyCommandValidator()
    {
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

public class CreateCompanyCommandHandler : IRequestHandler<CreateCompanyCommand, Result<Guid>>
{
    private readonly IApplicationDbContext _context;
    private readonly IValidator<CreateCompanyCommand> _validator;

    public CreateCompanyCommandHandler(IApplicationDbContext context, IValidator<CreateCompanyCommand> validator)
    {
        _context = context;
        _validator = validator;
    }

    public async Task<Result<Guid>> Handle(CreateCompanyCommand request, CancellationToken cancellationToken)
    {
        var validationResult = await _validator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            return Result.Failure<Guid>(Error.Validation("Company.Validation", validationResult.Errors.First().ErrorMessage));
        }

        // Verificar si ya existe una empresa con ese RFC
        var exists = await _context.Companies.AnyAsync(c => c.RFC == request.RFC.Trim().ToUpperInvariant(), cancellationToken);
        if (exists)
        {
            return Result.Failure<Guid>(Error.Conflict("Company.DuplicateRFC", $"Ya existe una empresa registrada con el RFC '{request.RFC}'."));
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
            var company = new Company(
                request.Name,
                request.RFC,
                address,
                request.Phone,
                request.Email
            );

            _context.Companies.Add(company);
            await _context.SaveChangesAsync(cancellationToken);

            return Result.Success(company.Id);
        }
        catch (Exception ex)
        {
            return Result.Failure<Guid>(Error.Failure("Company.CreateFailed", ex.Message));
        }
    }
}
