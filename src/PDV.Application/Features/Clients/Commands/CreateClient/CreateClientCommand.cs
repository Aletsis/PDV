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
    public string? FiscalRegime { get; set; }
    public string? CfdiUse { get; set; }
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
    private readonly IGeocodingService _geocodingService;

    public CreateClientCommandHandler(
        IApplicationDbContext context, 
        IComercialApiSyncService comercialSyncService,
        IGeocodingService geocodingService)
    {
        _context = context;
        _comercialSyncService = comercialSyncService;
        _geocodingService = geocodingService;
    }

    public async Task<Guid> Handle(CreateClientCommand request, CancellationToken cancellationToken)
    {
        var entity = new Client(
            request.Code,
            request.Name,
            request.TaxId,
            request.Phone,
            request.Email,
            fiscalRegime: request.FiscalRegime,
            fiscalZipCode: !string.IsNullOrWhiteSpace(request.ZipCode) && request.ZipCode != "00000" ? request.ZipCode : null,
            cfdiUse: request.CfdiUse
        );

        var street = !string.IsNullOrWhiteSpace(request.Street) ? request.Street : request.Address;
        if (!string.IsNullOrWhiteSpace(street) || !string.IsNullOrWhiteSpace(request.Colony))
        {
            var city = !string.IsNullOrWhiteSpace(request.City) ? request.City : "N/A";
            var state = !string.IsNullOrWhiteSpace(request.State) ? request.State : "N/A";
            var zipCode = !string.IsNullOrWhiteSpace(request.ZipCode) ? request.ZipCode : "00000";
            var country = !string.IsNullOrWhiteSpace(request.Country) ? request.Country : "México";

            var addressObj = Address.Create(
                street: string.IsNullOrWhiteSpace(street) ? "N/A" : street,
                city: city,
                state: state,
                zipCode: zipCode,
                country: country,
                exteriorNumber: request.ExteriorNumber,
                interiorNumber: request.InteriorNumber,
                colony: request.Colony
            );
            entity.UpdateAddress(addressObj);

            // Geolocalizar y resolver zona automáticamente usando la dirección estructurada completa
            var addressQuery = addressObj.ToFullAddressString();
            if (string.IsNullOrWhiteSpace(addressQuery))
                addressQuery = street;

            var (lat, lon) = await _geocodingService.GeocodeAddressQueryAsync(addressQuery, cancellationToken);
            if (lat.HasValue && lon.HasValue)
            {
                entity.SetCoordinates(lat.Value, lon.Value);

                var zones = await _context.DeliveryZones
                    .Where(z => z.IsActive)
                    .ToListAsync(cancellationToken);

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
                                entity.AssignDeliveryZone(zone.Id);
                                break;
                            }
                        }
                    }
                    catch
                    {
                        // Resiliencia ante errores de deserialización
                    }
                }
            }
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
