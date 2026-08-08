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
    public string Street { get; set; } = string.Empty;
    public string? ExteriorNumber { get; set; }
    public string? InteriorNumber { get; set; }
    public string? Colony { get; set; }
    public string ZipCode { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public string Country { get; set; } = "México";
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
    private readonly IGeocodingService _geocodingService;

    public UpdateClientCommandHandler(
        IApplicationDbContext context, 
        IComercialApiSyncService comercialSyncService,
        IGeocodingService geocodingService)
    {
        _context = context;
        _comercialSyncService = comercialSyncService;
        _geocodingService = geocodingService;
    }

    public async Task<bool> Handle(UpdateClientCommand request, CancellationToken cancellationToken)
    {
        var entity = await _context.Clients.FindAsync(new object[] { request.Id }, cancellationToken);

        if (entity == null)
            return false;

        entity.ChangeCode(request.Code);
        entity.UpdateProfile(request.Name, request.TaxId);
        entity.UpdateContactInfo(request.Phone, request.Email);

        var street = !string.IsNullOrWhiteSpace(request.Street) ? request.Street : request.Address;
        if (!string.IsNullOrWhiteSpace(street) || !string.IsNullOrWhiteSpace(request.Colony))
        {
            var city = !string.IsNullOrWhiteSpace(request.City) ? request.City : "N/A";
            var state = !string.IsNullOrWhiteSpace(request.State) ? request.State : "N/A";
            var zipCode = !string.IsNullOrWhiteSpace(request.ZipCode) ? request.ZipCode : "00000";
            var country = !string.IsNullOrWhiteSpace(request.Country) ? request.Country : "México";

            var newAddress = Address.Create(
                street: string.IsNullOrWhiteSpace(street) ? "N/A" : street,
                city: city,
                state: state,
                zipCode: zipCode,
                country: country,
                exteriorNumber: request.ExteriorNumber,
                interiorNumber: request.InteriorNumber,
                colony: request.Colony
            );

            bool addressChanged = entity.Address == null || entity.Address != newAddress;

            entity.UpdateAddress(newAddress);

            if (addressChanged)
            {
                var addressQuery = newAddress.ToFullAddressString();
                if (string.IsNullOrWhiteSpace(addressQuery))
                    addressQuery = street;

                var (lat, lon) = await _geocodingService.GeocodeAddressQueryAsync(addressQuery, cancellationToken);
                if (lat.HasValue && lon.HasValue)
                {
                    entity.SetCoordinates(lat.Value, lon.Value);

                    var zones = await _context.DeliveryZones
                        .Where(z => z.IsActive)
                        .ToListAsync(cancellationToken);

                    Guid? matchedZoneId = null;
                    foreach (var zone in zones)
                    {
                        try
                        {
                            var coordList = System.Text.Json.JsonSerializer.Deserialize<List<List<double>>>(zone.PolygonCoordinatesJson);
                            if (coordList != null && coordList.Count >= 3)
                            {
                                var polygon = coordList.Select(c => (c[0], c[1])).ToList();
                                if (_geocodingService.IsPointInPolygon(lat.Value, lon.Value, polygon))
                                {
                                    matchedZoneId = zone.Id;
                                    break;
                                }
                            }
                        }
                        catch
                        {
                            // Resiliencia
                        }
                    }
                    entity.AssignDeliveryZone(matchedZoneId);
                }
                else
                {
                    entity.SetCoordinates(null, null);
                    entity.AssignDeliveryZone(null);
                }
            }
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
