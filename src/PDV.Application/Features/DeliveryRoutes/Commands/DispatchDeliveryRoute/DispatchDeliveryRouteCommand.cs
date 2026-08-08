using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using PDV.Application.Common.Interfaces;
using PDV.Domain.Exceptions;
using PDV.Domain.Repositories;

namespace PDV.Application.Features.DeliveryRoutes.Commands.DispatchDeliveryRoute;

public record DispatchDeliveryRouteCommand : IRequest<bool>
{
    public Guid RouteId { get; set; }
}

public class DispatchDeliveryRouteCommandHandler : IRequestHandler<DispatchDeliveryRouteCommand, bool>
{
    private readonly IDeliveryRouteRepository _routeRepository;
    private readonly IOrderRepository _orderRepository;
    private readonly IApplicationDbContext _context;

    public DispatchDeliveryRouteCommandHandler(
        IDeliveryRouteRepository routeRepository,
        IOrderRepository orderRepository,
        IApplicationDbContext context)
    {
        _routeRepository = routeRepository;
        _orderRepository = orderRepository;
        _context = context;
    }

    public async Task<bool> Handle(DispatchDeliveryRouteCommand request, CancellationToken cancellationToken)
    {
        await _context.BeginTransactionAsync(cancellationToken);

        try
        {
            var route = await _routeRepository.GetByIdWithOrdersAsync(request.RouteId, cancellationToken);
            if (route == null)
            {
                throw new DomainException("Ruta de entrega no encontrada.");
            }

            route.Dispatch();

            await _routeRepository.UpdateAsync(route, cancellationToken);
            
            // Actualizar el estado de los pedidos que pertenecen a la ruta
            foreach (var order in route.Orders)
            {
                await _orderRepository.UpdateAsync(order, cancellationToken);
            }

            await _context.SaveChangesAsync(cancellationToken);
            await _context.CommitTransactionAsync(cancellationToken);

            return true;
        }
        catch
        {
            await _context.RollbackTransactionAsync(cancellationToken);
            throw;
        }
    }
}
