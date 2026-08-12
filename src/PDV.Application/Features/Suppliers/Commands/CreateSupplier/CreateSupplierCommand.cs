using System;
using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PDV.Application.Common.Interfaces;
using PDV.Domain.Entities;
using PDV.Domain.Exceptions;
using PDV.Domain.ValueObjects;

namespace PDV.Application.Features.Suppliers.Commands.CreateSupplier;

public record CreateSupplierCommand : IRequest<Guid>
{
    public string Code { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string TaxId { get; init; } = string.Empty;
    public string? Phone { get; init; }
    public string? Email { get; init; }
    public string? Street { get; init; }
    public string? ExteriorNumber { get; init; }
    public string? InteriorNumber { get; init; }
    public string? Colony { get; init; }
    public string? City { get; init; }
    public string? State { get; init; }
    public string? ZipCode { get; init; }
    public string? Country { get; init; }
}

public class CreateSupplierCommandValidator : AbstractValidator<CreateSupplierCommand>
{
    public CreateSupplierCommandValidator()
    {
        RuleFor(v => v.Code)
            .NotEmpty().WithMessage("El código del proveedor es obligatorio.")
            .MaximumLength(30).WithMessage("El código no puede exceder 30 caracteres.");

        RuleFor(v => v.Name)
            .NotEmpty().WithMessage("El nombre o razón social es obligatorio.")
            .MaximumLength(150).WithMessage("El nombre no puede exceder 150 caracteres.");
    }
}

public class CreateSupplierCommandHandler : IRequestHandler<CreateSupplierCommand, Guid>
{
    private readonly IApplicationDbContext _context;
    private readonly IComercialApiSyncService _comercialSyncService;

    public CreateSupplierCommandHandler(
        IApplicationDbContext context,
        IComercialApiSyncService comercialSyncService)
    {
        _context = context;
        _comercialSyncService = comercialSyncService;
    }

    public async Task<Guid> Handle(CreateSupplierCommand request, CancellationToken cancellationToken)
    {
        var code = request.Code.Trim().ToUpperInvariant();
        var exists = await _context.Suppliers.AnyAsync(s => s.Code == code, cancellationToken);
        if (exists)
        {
            throw new DomainException($"Ya existe un proveedor registrado con el código '{code}'.");
        }

        Address? address = null;
        if (!string.IsNullOrWhiteSpace(request.Street) || !string.IsNullOrWhiteSpace(request.Colony) || !string.IsNullOrWhiteSpace(request.ZipCode))
        {
            address = new Address(
                street: request.Street ?? string.Empty,
                exteriorNumber: request.ExteriorNumber ?? string.Empty,
                interiorNumber: request.InteriorNumber,
                colony: request.Colony ?? string.Empty,
                city: request.City ?? string.Empty,
                state: request.State ?? string.Empty,
                zipCode: request.ZipCode ?? string.Empty,
                country: string.IsNullOrWhiteSpace(request.Country) ? "México" : request.Country);
        }

        var supplier = new Supplier(
            code: code,
            name: request.Name,
            taxId: request.TaxId,
            phone: request.Phone,
            email: request.Email,
            address: address);

        _context.Suppliers.Add(supplier);

        // Encolar para sincronización diferida resiliente
        var syncTask = new ContpaqiSyncQueue(supplier.Id, "Supplier", "Create");
        _context.ContpaqiSyncQueues.Add(syncTask);

        await _context.SaveChangesAsync(cancellationToken);

        // Intento inmediato de sincronización
        _ = Task.Run(async () =>
        {
            try
            {
                await _comercialSyncService.SendSupplierToComercialAsync(supplier, CancellationToken.None);
            }
            catch
            {
                // Dejar que el background worker reintente
            }
        }, CancellationToken.None);

        return supplier.Id;
    }
}
