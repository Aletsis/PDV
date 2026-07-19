using FluentValidation;
using MediatR;
using PDV.Application.Common.Interfaces;
using PDV.Domain.Entities;

using Microsoft.EntityFrameworkCore;

namespace PDV.Application.Features.Clients.Commands.UpdateClient;

using PDV.Domain.ValueObjects;

public record UpdateClientCommand : IRequest<bool>
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string TaxId { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
}

public class UpdateClientCommandValidator : AbstractValidator<UpdateClientCommand>
{
    public UpdateClientCommandValidator()
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

public class UpdateClientCommandHandler : IRequestHandler<UpdateClientCommand, bool>
{
    private readonly IApplicationDbContext _context;
    private readonly IComercialApiSyncService _comercialSyncService;

    public UpdateClientCommandHandler(IApplicationDbContext context, IComercialApiSyncService comercialSyncService)
    {
        _context = context;
        _comercialSyncService = comercialSyncService;
    }

    public async Task<bool> Handle(UpdateClientCommand request, CancellationToken cancellationToken)
    {
        var entity = await _context.Clients.FindAsync(new object[] { request.Id }, cancellationToken);

        if (entity == null)
            return false;

        entity.ChangeCode(request.Code);
        entity.UpdateProfile(request.Name, request.TaxId);
        entity.UpdateContactInfo(request.Phone, request.Email);

        if (!string.IsNullOrWhiteSpace(request.Address))
        {
            entity.UpdateAddress(Address.Create(request.Address, "N/A", "N/A", "00000", "México"));
        }

        if (request.IsActive && !entity.IsActive)
        {
            entity.Activate();
        }
        else if (!request.IsActive && entity.IsActive)
        {
            entity.Deactivate();
        }

        await _context.SaveChangesAsync(cancellationToken);

        // Sincronizar de forma diferida con Comercial si estamos en el servidor (no SQLite)
        if (_context is DbContext dbContext && dbContext.Database.ProviderName?.Contains("Sqlite", StringComparison.OrdinalIgnoreCase) == false)
        {
            try
            {
                var queueItem = new ContpaqiSyncQueue(entity.Id, "Client", "Update");
                _context.ContpaqiSyncQueues.Add(queueItem);
                await _context.SaveChangesAsync(cancellationToken);
            }
            catch (Exception)
            {
                // Resiliencia
            }
        }

        return true;
    }
}
