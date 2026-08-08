using MediatR;
using PDV.Application.Common.Interfaces;
using PDV.Application.Features.Clients.Dtos;
using Microsoft.EntityFrameworkCore;

namespace PDV.Application.Features.Clients.Queries.GetClient;

public record GetClientQuery(Guid Id) : IRequest<ClientDto?>;

public class GetClientQueryHandler : IRequestHandler<GetClientQuery, ClientDto?>
{
    private readonly IApplicationDbContext _context;

    public GetClientQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<ClientDto?> Handle(GetClientQuery request, CancellationToken cancellationToken)
    {
        var client = await _context.Clients
            .Include(c => c.DeliveryZone)
            .FirstOrDefaultAsync(c => c.Id == request.Id, cancellationToken);

        if (client == null)
            return null;

        return new ClientDto
        {
            Id = client.Id,
            Code = client.Code,
            Name = client.Name,
            TaxId = client.TaxId,
            Address = client.Address != null ? client.Address.ToFullAddressString() : string.Empty,
            Street = client.Address != null ? client.Address.Street : string.Empty,
            ExteriorNumber = client.Address?.ExteriorNumber,
            InteriorNumber = client.Address?.InteriorNumber,
            Colony = client.Address?.Colony,
            ZipCode = client.Address != null ? client.Address.ZipCode : string.Empty,
            City = client.Address != null ? client.Address.City : string.Empty,
            State = client.Address != null ? client.Address.State : string.Empty,
            Country = client.Address != null ? client.Address.Country : "México",
            Phone = client.Phone,
            Email = client.Email,
            IsActive = client.IsActive,
            ClientType = client.ClientType,
            FiscalRegime = client.FiscalRegime,
            FiscalZipCode = client.FiscalZipCode,
            Latitude = client.Latitude,
            Longitude = client.Longitude,
            DeliveryZoneId = client.DeliveryZoneId,
            DeliveryZoneName = client.DeliveryZone != null ? client.DeliveryZone.Name : null
        };
    }
}
