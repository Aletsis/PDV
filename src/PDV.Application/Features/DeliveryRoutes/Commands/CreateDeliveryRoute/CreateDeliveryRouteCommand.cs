using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PDV.Application.Common.Interfaces;
using PDV.Domain.Entities;
using PDV.Domain.Exceptions;
using PDV.Domain.Repositories;

namespace PDV.Application.Features.DeliveryRoutes.Commands.CreateDeliveryRoute;

public record CreateDeliveryRouteCommand : IRequest<Guid>
{
    public Guid BranchId { get; set; }
    public Guid? DeliveryZoneId { get; set; }
    public string DeliveryManId { get; set; } = string.Empty;
    public List<Guid> OrderIds { get; set; } = new();
    public string CreatedBy { get; set; } = "system";
}

public class CreateDeliveryRouteCommandHandler : IRequestHandler<CreateDeliveryRouteCommand, Guid>
{
    private readonly IDeliveryRouteRepository _routeRepository;
    private readonly IOrderRepository _orderRepository;
    private readonly IIdentityService _identityService;
    private readonly IApplicationDbContext _context;

    public CreateDeliveryRouteCommandHandler(
        IDeliveryRouteRepository routeRepository,
        IOrderRepository orderRepository,
        IIdentityService identityService,
        IApplicationDbContext context)
    {
        _routeRepository = routeRepository;
        _orderRepository = orderRepository;
        _identityService = identityService;
        _context = context;
    }

    public async Task<Guid> Handle(CreateDeliveryRouteCommand request, CancellationToken cancellationToken)
    {
        await _context.BeginTransactionAsync(cancellationToken);

        try
        {
            if (string.IsNullOrWhiteSpace(request.DeliveryManId))
            {
                throw new DomainException("El repartidor es requerido.");
            }

            if (request.OrderIds == null || request.OrderIds.Count == 0)
            {
                throw new DomainException("Debe seleccionar al menos un pedido para crear la ruta.");
            }

            // Validar que el repartidor exista en el sistema
            var deliveryMan = await _identityService.GetUserByIdAsync(request.DeliveryManId, cancellationToken);
            if (deliveryMan == null)
            {
                throw new DomainException("Repartidor no encontrado en el sistema.");
            }

            // Validar rol repartidor
            var isDeliveryMan = deliveryMan.Roles.Any(r => 
                r.Equals("DeliveryMan", StringComparison.OrdinalIgnoreCase) || 
                r.Equals("repartidor", StringComparison.OrdinalIgnoreCase));
                
            if (!isDeliveryMan)
            {
                throw new DomainException("El usuario seleccionado no tiene el rol de repartidor.");
            }

            // Validar que pertenezca a la misma sucursal
            if (!deliveryMan.BranchId.HasValue || deliveryMan.BranchId.Value != request.BranchId)
            {
                throw new DomainException("El repartidor debe pertenecer a la misma sucursal de la ruta.");
            }

            int nextFolio = await _routeRepository.GetNextFolioAsync(request.BranchId, cancellationToken);

            var route = new DeliveryRoute(
                request.BranchId,
                request.DeliveryZoneId,
                request.DeliveryManId,
                nextFolio
            );

            // Asignar el creador para auditoría
            route.SetCreationAudit(request.CreatedBy);

            // Cargar y agregar los pedidos
            foreach (var orderId in request.OrderIds)
            {
                var order = await _orderRepository.GetByIdAsync(orderId, cancellationToken);
                if (order == null)
                {
                    throw new DomainException($"Pedido con ID {orderId} no encontrado.");
                }

                if (order.BranchId != request.BranchId)
                {
                    throw new DomainException($"El pedido con ID {orderId} (Folio {order.Series}-{order.Folio}) no pertenece a esta sucursal.");
                }

                route.AddOrder(order);
                await _orderRepository.UpdateAsync(order, cancellationToken);
            }

            await _routeRepository.AddAsync(route, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);
            await _context.CommitTransactionAsync(cancellationToken);

            return route.Id;
        }
        catch
        {
            await _context.RollbackTransactionAsync(cancellationToken);
            throw;
        }
    }
}
