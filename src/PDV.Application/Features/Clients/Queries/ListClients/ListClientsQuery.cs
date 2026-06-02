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
        var query = _context.Clients.AsQueryable();

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
                Phone = c.Phone,
                Email = c.Email,
                IsActive = c.IsActive,
                ClientType = c.ClientType
            })
            .ToListAsync(cancellationToken);
    }
}
