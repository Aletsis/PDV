using MediatR;
using PDV.Application.Common.Interfaces;
using PDV.Application.Features.Clients.Dtos;
using Microsoft.EntityFrameworkCore;

namespace PDV.Application.Features.Clients.Queries.ListClients;

public record ListClientsQuery(bool IncludeInactive = false) : IRequest<List<ClientDto>>;

public class ListClientsQueryHandler : IRequestHandler<ListClientsQuery, List<ClientDto>>
{
    private readonly IApplicationDbContext _context;

    public ListClientsQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<ClientDto>> Handle(ListClientsQuery request, CancellationToken cancellationToken)
    {
        var query = _context.Clients
            .Include(c => c.DeliveryZone)
            .AsQueryable();

        if (!request.IncludeInactive)
        {
            query = query.Where(c => c.IsActive);
        }

        return await query
            .Select(c => new ClientDto
            {
                Id = c.Id,
                Code = c.Code,
                Name = c.Name,
                TaxId = c.TaxId,
                Address = c.Address != null ? c.Address.Street : string.Empty,
                Street = c.Address != null ? c.Address.Street : string.Empty,
                ExteriorNumber = c.Address != null ? c.Address.ExteriorNumber : null,
                InteriorNumber = c.Address != null ? c.Address.InteriorNumber : null,
                Colony = c.Address != null ? c.Address.Colony : null,
                ZipCode = c.Address != null ? c.Address.ZipCode : string.Empty,
                City = c.Address != null ? c.Address.City : string.Empty,
                State = c.Address != null ? c.Address.State : string.Empty,
                Country = c.Address != null ? c.Address.Country : "México",
                Phone = c.Phone,
                Email = c.Email,
                IsActive = c.IsActive,
                ClientType = c.ClientType,
                FiscalRegime = c.FiscalRegime,
                FiscalZipCode = c.FiscalZipCode,
                CfdiUse = c.CfdiUse,
                Latitude = c.Latitude,
                Longitude = c.Longitude,
                DeliveryZoneId = c.DeliveryZoneId,
                DeliveryZoneName = c.DeliveryZone != null ? c.DeliveryZone.Name : null
            })
            .ToListAsync(cancellationToken);
    }
}
