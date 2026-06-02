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
        var client = await _context.Clients.FindAsync(new object[] { request.Id }, cancellationToken);

        if (client == null)
            return null;

        return new ClientDto
        {
            Id = client.Id,
            Code = client.Code,
            Name = client.Name,
            TaxId = client.TaxId,
            Address = client.Address != null ? client.Address.Street : string.Empty,
            Phone = client.Phone,
            Email = client.Email,
            IsActive = client.IsActive,
            ClientType = client.ClientType
        };
    }
}
