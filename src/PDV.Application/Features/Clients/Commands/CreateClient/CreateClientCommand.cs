using FluentValidation;
using MediatR;
using PDV.Application.Common.Interfaces;
using PDV.Domain.Entities;
using PDV.Domain.ValueObjects;

using Microsoft.EntityFrameworkCore;

namespace PDV.Application.Features.Clients.Commands.CreateClient;


public record CreateClientCommand : IRequest<Guid>
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string TaxId { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
}

public class CreateClientCommandValidator : AbstractValidator<CreateClientCommand>
{
    public CreateClientCommandValidator()
    {
        RuleFor(v => v.Code)
            .NotEmpty().WithMessage("El código del cliente es requerido")
            .MaximumLength(30);

        RuleFor(v => v.Name)
            .NotEmpty().WithMessage("El nombre es requerido")
            .MaximumLength(100);

        RuleFor(v => v.TaxId)
            .NotEmpty().WithMessage("El RFC/ID Fiscal es requerido")
            .MaximumLength(50);

        RuleFor(v => v.Email)
            .EmailAddress().WithMessage("Email inválido")
            .When(x => !string.IsNullOrWhiteSpace(x.Email));
    }
}

public class CreateClientCommandHandler : IRequestHandler<CreateClientCommand, Guid>
{
    private readonly IApplicationDbContext _context;
    private readonly IComercialApiSyncService _comercialSyncService;

    public CreateClientCommandHandler(IApplicationDbContext context, IComercialApiSyncService comercialSyncService)
    {
        _context = context;
        _comercialSyncService = comercialSyncService;
    }

    public async Task<Guid> Handle(CreateClientCommand request, CancellationToken cancellationToken)
    {
        var entity = new Client(
            request.Code,
            request.Name,
            request.TaxId,
            request.Phone,
            request.Email
        );

        if (!string.IsNullOrWhiteSpace(request.Address))
        {
            entity.UpdateAddress(Address.Create(request.Address, "N/A", "N/A", "00000", "México"));
        }

        _context.Clients.Add(entity);
        await _context.SaveChangesAsync(cancellationToken);

        // Sincronizar de forma diferida con Comercial si estamos en el servidor (no SQLite)
        if (_context is DbContext dbContext && dbContext.Database.ProviderName?.Contains("Sqlite", StringComparison.OrdinalIgnoreCase) == false)
        {
            try
            {
                var queueItem = new ContpaqiSyncQueue(entity.Id, "Client", "Create");
                _context.ContpaqiSyncQueues.Add(queueItem);
                await _context.SaveChangesAsync(cancellationToken);
            }
            catch (Exception)
            {
                // Resiliencia
            }
        }

        return entity.Id;
    }
}
