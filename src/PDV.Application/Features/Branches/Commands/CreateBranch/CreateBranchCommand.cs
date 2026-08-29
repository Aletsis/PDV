using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PDV.Application.Common.Interfaces;
using PDV.Domain.Entities;
using PDV.Domain.Repositories;
using PDV.Domain.ValueObjects;

namespace PDV.Application.Features.Branches.Commands.CreateBranch;

public record CreateBranchCommand(
    string Name,
    string Code,
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
    bool IsMainBranch = false,
    double? Latitude = null,
    double? Longitude = null,
    string? Address = null
) : IRequest<Guid>;

public class CreateBranchCommandHandler : IRequestHandler<CreateBranchCommand, Guid>
{
    private readonly IBranchRepository _repository;
    private readonly IApplicationDbContext _context;
    private readonly IGeocodingService _geocodingService;

    public CreateBranchCommandHandler(
        IBranchRepository repository, 
        IApplicationDbContext context,
        IGeocodingService geocodingService)
    {
        _repository = repository;
        _context = context;
        _geocodingService = geocodingService;
    }

    public async Task<Guid> Handle(CreateBranchCommand request, CancellationToken cancellationToken)
    {
        // Validar código único
        var existing = await _repository.GetByCodeAsync(request.Code, cancellationToken);
        if (existing != null)
            throw new InvalidOperationException($"Ya existe una sucursal con el código '{request.Code}'");

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

        var branch = new Branch(
            request.Name,
            request.Code,
            addressObj,
            request.Phone,
            request.Email,
            request.IsMainBranch,
            latitude,
            longitude
        );

        await _repository.AddAsync(branch, cancellationToken);

        // Inicializar ProductBranchStock para todos los productos existentes en la nueva sucursal
        var productIds = await _context.Products.AsNoTracking().Select(p => p.Id).ToListAsync(cancellationToken);
        foreach (var productId in productIds)
        {
            var branchStock = new ProductBranchStock(productId, branch.Id, 0m, 0m);
            _context.ProductBranchStocks.Add(branchStock);
        }

        if (productIds.Count > 0)
        {
            await _context.SaveChangesAsync(cancellationToken);
        }

        return branch.Id;
    }
}
