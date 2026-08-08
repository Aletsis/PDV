using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using PDV.Application.Common.Interfaces;
using PDV.Domain.Exceptions;
using PDV.Domain.Repositories;

namespace PDV.Application.Features.DeliveryZones.Commands.DeleteDeliveryZone;

public record DeleteDeliveryZoneCommand(Guid Id) : IRequest<bool>;

public class DeleteDeliveryZoneCommandHandler : IRequestHandler<DeleteDeliveryZoneCommand, bool>
{
    private readonly IDeliveryZoneRepository _zoneRepository;
    private readonly IApplicationDbContext _context;

    public DeleteDeliveryZoneCommandHandler(IDeliveryZoneRepository zoneRepository, IApplicationDbContext context)
    {
        _zoneRepository = zoneRepository;
        _context = context;
    }

    public async Task<bool> Handle(DeleteDeliveryZoneCommand request, CancellationToken cancellationToken)
    {
        var zone = await _zoneRepository.GetByIdAsync(request.Id, cancellationToken);
        if (zone == null)
        {
            throw new DomainException("Zona de reparto no encontrada.");
        }

        // En lugar de borrar físicamente, desactivamos o borramos suavemente
        await _zoneRepository.DeleteAsync(zone, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);

        return true;
    }
}
