using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PDV.Application.Common.Interfaces;
using PDV.Domain.Entities;
using PDV.Domain.Enums;
using PDV.Domain.Exceptions;
using PDV.Domain.Repositories;

namespace PDV.Application.Features.Orders.Commands.VerifyAndConfirmOrder;

public record VerifyOrderItemDto
{
    public Guid ItemId { get; set; }
    public decimal RealQuantity { get; set; }
    public decimal? PriceOverride { get; set; }
}

public record VerifyAndConfirmOrderCommand : IRequest<bool>
{
    public Guid OrderId { get; set; }
    public string UserId { get; set; } = string.Empty;
    public Guid CashRegisterId { get; set; }
    public Guid ShiftId { get; set; }
    public Guid? DeliveryZoneId { get; set; }
    public string? GeneralNotes { get; set; }
    public string? DeliveryNotes { get; set; }
    public List<VerifyOrderItemDto> UpdatedItems { get; set; } = new();
}

public class VerifyAndConfirmOrderCommandHandler : IRequestHandler<VerifyAndConfirmOrderCommand, bool>
{
    private readonly IApplicationDbContext _context;
    private readonly IOrderRepository _orderRepository;

    public VerifyAndConfirmOrderCommandHandler(
        IApplicationDbContext context,
        IOrderRepository orderRepository)
    {
        _context = context;
        _orderRepository = orderRepository;
    }

    public async Task<bool> Handle(VerifyAndConfirmOrderCommand request, CancellationToken cancellationToken)
    {
        await _context.BeginTransactionAsync(cancellationToken);

        try
        {
            var order = await _context.Orders
                .Include(o => o.Items)
                .FirstOrDefaultAsync(o => o.Id == request.OrderId, cancellationToken);

            if (order == null)
                throw new DomainException("Pedido no encontrado.");

            // Si el pedido no tenía folio o serie asignado, asignarlo desde la secuencia de la sucursal
            if (order.Folio <= 0)
            {
                int nextFolio = await _orderRepository.GetNextFolioAsync(order.BranchId, cancellationToken);
                string series = string.IsNullOrWhiteSpace(order.Series) || order.Series == "TEL" ? "PED" : order.Series;
                order.SetFolio(series, nextFolio);
            }

            // Actualizar pesos reales o cantidades ajustadas
            foreach (var itemUpdate in request.UpdatedItems)
            {
                var item = order.Items.FirstOrDefault(i => i.Id == itemUpdate.ItemId);
                if (item != null)
                {
                    if (itemUpdate.RealQuantity > 0)
                    {
                        item.SetVerifiedQuantity(itemUpdate.RealQuantity);
                    }
                    if (itemUpdate.PriceOverride.HasValue)
                    {
                        item.OverridePrice(itemUpdate.PriceOverride.Value);
                    }
                }
            }

            if (!string.IsNullOrWhiteSpace(request.GeneralNotes))
            {
                order.SetGeneralNotes(request.GeneralNotes);
            }

            if (!string.IsNullOrWhiteSpace(request.DeliveryNotes))
            {
                order.SetDeliveryNotes(request.DeliveryNotes);
            }

            if (request.DeliveryZoneId.HasValue)
            {
                order.SetDeliveryZone(request.DeliveryZoneId.Value);
            }

            // Verificar y confirmar en caja
            order.VerifyOrder(request.UserId, request.CashRegisterId, request.ShiftId);

            // Intentar auto-asignar a ruta si tiene zona de reparto configurada
            if (order.DeliveryZoneId.HasValue)
            {
                var openRoute = await _context.DeliveryRoutes
                    .Include(r => r.Orders)
                    .FirstOrDefaultAsync(r => r.BranchId == order.BranchId &&
                                              r.DeliveryZoneId == order.DeliveryZoneId &&
                                              r.Status == DeliveryRouteStatus.Created, cancellationToken);

                if (openRoute != null && openRoute.Orders.Count < 5)
                {
                    openRoute.AddOrder(order);
                }
                else if (openRoute == null)
                {
                    // Crear nueva ruta para la zona
                    var nextRouteFolio = (await _context.DeliveryRoutes
                        .Where(r => r.BranchId == order.BranchId)
                        .MaxAsync(r => (int?)r.Folio, cancellationToken) ?? 0) + 1;

                    var newRoute = new DeliveryRoute(order.BranchId, order.DeliveryZoneId, null, nextRouteFolio);
                    newRoute.AddOrder(order);
                    _context.DeliveryRoutes.Add(newRoute);
                }
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
