using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using PDV.Application.Common.Interfaces;
using PDV.Domain.Repositories;
using PDV.Domain.ValueObjects;

namespace PDV.Application.Features.Branches.Commands.UpdateBranch;

public record UpdateBranchCommand(
    Guid Id,
    string Name,
    string Street,
    string Phone,
    string? ExteriorNumber = null,
    string? InteriorNumber = null,
    string? Colony = null,
    string? ZipCode = null,
    string? City = null,
    string? State = null,
    string? Country = "México",
    string? Email = null,
    double? Latitude = null,
    double? Longitude = null,
    string? Address = null
) : IRequest;

public class UpdateBranchCommandHandler : IRequestHandler<UpdateBranchCommand>
{
    private readonly IBranchRepository _repository;
    private readonly IGeocodingService _geocodingService;

    public UpdateBranchCommandHandler(
        IBranchRepository repository,
        IGeocodingService geocodingService)
    {
        _repository = repository;
        _geocodingService = geocodingService;
    }

    public async Task Handle(UpdateBranchCommand request, CancellationToken cancellationToken)
    {
        var branch = await _repository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new KeyNotFoundException($"Sucursal con ID {request.Id} no encontrada");

        Address? addressObj = null;
        var street = !string.IsNullOrWhiteSpace(request.Street) ? request.Street : request.Address;
        if (!string.IsNullOrWhiteSpace(street) || !string.IsNullOrWhiteSpace(request.Colony))
        {
            var city = !string.IsNullOrWhiteSpace(request.City) ? request.City : "N/A";
            var state = !string.IsNullOrWhiteSpace(request.State) ? request.State : "N/A";
            var zipCode = !string.IsNullOrWhiteSpace(request.ZipCode) ? request.ZipCode : "00000";
            var country = !string.IsNullOrWhiteSpace(request.Country) ? request.Country : "México";

            addressObj = Domain.ValueObjects.Address.Create(
                street: string.IsNullOrWhiteSpace(street) ? "N/A" : street,
                city: city,
                state: state,
                zipCode: zipCode,
                country: country,
                exteriorNumber: request.ExteriorNumber,
                interiorNumber: request.InteriorNumber,
                colony: request.Colony
            );
        }

        double? latitude = request.Latitude;
        double? longitude = request.Longitude;

        // Si no se proporcionaron coordenadas pero sí dirección, intentar geocodificar automáticamente
        if ((!latitude.HasValue || !longitude.HasValue) && addressObj != null)
        {
            var addressQuery = addressObj.ToFullAddressString();
            if (string.IsNullOrWhiteSpace(addressQuery))
                addressQuery = street;

            if (!string.IsNullOrWhiteSpace(addressQuery))
            {
                var (gLat, gLon) = await _geocodingService.GeocodeAddressQueryAsync(addressQuery, cancellationToken);
                if (gLat.HasValue && gLon.HasValue)
                {
                    latitude = gLat.Value;
                    longitude = gLon.Value;
                }
            }
        }

        branch.Update(request.Name, addressObj, request.Phone, request.Email, latitude, longitude);
        await _repository.UpdateAsync(branch, cancellationToken);
    }
}
