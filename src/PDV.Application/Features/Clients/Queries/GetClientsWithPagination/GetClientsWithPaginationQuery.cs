using MediatR;
using Microsoft.EntityFrameworkCore;
using PDV.Application.Common.Interfaces;
using PDV.Application.Common.Models;
using PDV.Application.Features.Clients.Dtos;

namespace PDV.Application.Features.Clients.Queries.GetClientsWithPagination;

public record GetClientsWithPaginationQuery : IRequest<PaginatedList<ClientDto>>
{
    public int PageNumber { get; init; } = 1;
    public int PageSize { get; init; } = 10;
    public string? SearchQuery { get; init; }
    public bool IncludeInactive { get; init; } = false;
}

public class GetClientsWithPaginationQueryHandler : IRequestHandler<GetClientsWithPaginationQuery, PaginatedList<ClientDto>>
{
    private readonly IApplicationDbContext _context;

    public GetClientsWithPaginationQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<PaginatedList<ClientDto>> Handle(GetClientsWithPaginationQuery request, CancellationToken cancellationToken)
    {
        var query = _context.Clients
            .Include(c => c.DeliveryZone)
            .AsNoTracking();

        if (!request.IncludeInactive)
        {
            query = query.Where(x => x.IsActive);
        }

        if (!string.IsNullOrWhiteSpace(request.SearchQuery))
        {
            var search = request.SearchQuery.Trim().ToLower();
            query = query.Where(x =>
                x.Name.ToLower().Contains(search) ||
                (x.TaxId != null && x.TaxId.ToLower().Contains(search)) ||
                (x.Phone != null && x.Phone.ToLower().Contains(search))
            );
        }

        var projection = query.Select(c => new ClientDto
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
        });

        return await PaginatedList<ClientDto>.CreateAsync(projection, request.PageNumber, request.PageSize, cancellationToken);
    }
}
