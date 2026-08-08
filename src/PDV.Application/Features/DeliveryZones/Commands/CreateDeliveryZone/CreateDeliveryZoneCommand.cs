using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using PDV.Application.Common.Interfaces;
using PDV.Domain.Entities;
using PDV.Domain.Repositories;

namespace PDV.Application.Features.DeliveryZones.Commands.CreateDeliveryZone;

public record CreateDeliveryZoneCommand : IRequest<Guid>
{
    public string Name { get; set; } = string.Empty;
    public Guid BranchId { get; set; }
    public string PolygonCoordinatesJson { get; set; } = string.Empty;
    public decimal DeliveryCost { get; set; }
}

public class CreateDeliveryZoneCommandHandler : IRequestHandler<CreateDeliveryZoneCommand, Guid>
{
    private readonly IDeliveryZoneRepository _zoneRepository;
    private readonly IApplicationDbContext _context;

    public CreateDeliveryZoneCommandHandler(IDeliveryZoneRepository zoneRepository, IApplicationDbContext context)
    {
        _zoneRepository = zoneRepository;
        _context = context;
    }

    public async Task<Guid> Handle(CreateDeliveryZoneCommand request, CancellationToken cancellationToken)
    {
        var zone = new DeliveryZone(
            request.Name,
            request.BranchId,
            request.PolygonCoordinatesJson,
            request.DeliveryCost
        );

        await _zoneRepository.AddAsync(zone, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);

        return zone.Id;
    }
}
