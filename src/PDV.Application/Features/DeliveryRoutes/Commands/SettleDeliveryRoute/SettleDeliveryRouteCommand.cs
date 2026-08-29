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
using PDV.Domain.ValueObjects;

namespace PDV.Application.Features.DeliveryRoutes.Commands.SettleDeliveryRoute;

public record OrderSettlementResultDto
{
    public Guid OrderId { get; set; }
    public bool Delivered { get; set; }
    public string? ReturnReason { get; set; }
}

public record SettleDeliveryRouteCommand : IRequest<bool>
{
    public Guid RouteId { get; set; }
    public Guid CashRegisterId { get; set; }
    public string UserId { get; set; } = string.Empty;
    public List<OrderSettlementResultDto> Settlements { get; set; } = new();
}

public class SettleDeliveryRouteCommandHandler : IRequestHandler<SettleDeliveryRouteCommand, bool>
{
    private readonly IDeliveryRouteRepository _routeRepository;
    private readonly IOrderRepository _orderRepository;
    private readonly ISaleRepository _saleRepository;
    private readonly ITicketSequenceRepository _ticketSequenceRepository;
    private readonly IApplicationDbContext _context;

    public SettleDeliveryRouteCommandHandler(
        IDeliveryRouteRepository routeRepository,
        IOrderRepository orderRepository,
        ISaleRepository saleRepository,
        ITicketSequenceRepository ticketSequenceRepository,
        IApplicationDbContext context)
    {
        _routeRepository = routeRepository;
        _orderRepository = orderRepository;
        _saleRepository = saleRepository;
        _ticketSequenceRepository = ticketSequenceRepository;
        _context = context;
    }

    public async Task<bool> Handle(SettleDeliveryRouteCommand request, CancellationToken cancellationToken)
    {
        const int maxRetries = 3;
        int attempt = 0;

        while (true)
        {
            attempt++;

            if (_context is DbContext dbContext)
            {
                dbContext.ChangeTracker.Clear();
            }

            await _context.BeginTransactionAsync(cancellationToken);

            try
            {
                var route = await _routeRepository.GetByIdWithOrdersAsync(request.RouteId, cancellationToken);
                if (route == null)
                {
                    throw new DomainException("Ruta de entrega no encontrada.");
                }

                if (route.Status != DeliveryRouteStatus.EnRoute)
                {
                    throw new DomainException("La ruta debe estar en camino para poder liquidarla.");
                }

                // Buscar turno activo para la caja registradora dada
                var activeShift = await _context.Shifts
                    .FirstOrDefaultAsync(s => s.CashRegisterId == request.CashRegisterId && s.Status == ShiftStatus.Open, cancellationToken);

                if (activeShift == null)
                {
                    throw new DomainException("No se puede liquidar la ruta si no hay un turno de caja activo en esta caja.");
                }

                // Procesar la liquidación
                route.Settle();

                // Procesar cada pedido
                foreach (var settleResult in request.Settlements)
                {
                    var order = route.Orders.FirstOrDefault(o => o.Id == settleResult.OrderId);
                    if (order == null)
                    {
                        throw new DomainException($"El pedido {settleResult.OrderId} no pertenece a esta ruta.");
                    }

                    var isDelivered = settleResult.Delivered || order.Status == OrderStatus.Delivered;

                    if (isDelivered)
                    {
                        if (order.Status == OrderStatus.EnRoute)
                        {
                            order.MarkAsDelivered();
                        }

                        // 2. Generar Venta (Sale) asociada al turno actual de la caja
                        var sequence = await _ticketSequenceRepository.GetWithLockAsync(request.CashRegisterId, TicketSequenceType.Sale, cancellationToken);
                        if (sequence == null)
                        {
                            sequence = new TicketSequence(request.CashRegisterId, TicketSequenceType.Sale, "V");
                            await _ticketSequenceRepository.AddAsync(sequence, cancellationToken);
                        }

                        int saleFolio = sequence.GetNextTicketNumber();
                        string saleSeries = sequence.Series ?? "V";

                        var sale = new Sale(
                            saleNumber: SaleNumber.Generate(),
                            paymentMethod: order.PaymentMethod,
                            userId: request.UserId,
                            shiftId: activeShift.Id,
                            series: saleSeries,
                            folio: saleFolio,
                            clientId: order.ClientId,
                            cashRegisterId: request.CashRegisterId
                        );
                        sale.SetBranch(order.BranchId);

                        await _ticketSequenceRepository.UpdateAsync(sequence, cancellationToken);

                        // Cargar ítems del pedido con detalles completos de producto
                        var orderWithItems = await _orderRepository.GetByIdWithItemsAsync(order.Id, cancellationToken);
                        if (orderWithItems == null)
                        {
                            throw new DomainException($"No se pudieron cargar los ítems para el pedido {order.Id}.");
                        }

                        foreach (var orderItem in orderWithItems.Items)
                        {
                            var product = await _context.Products.FindAsync(new object[] { orderItem.ProductId }, cancellationToken);
                            if (product == null)
                            {
                                throw new DomainException($"Producto {orderItem.ProductId} no encontrado al facturar pedido.");
                            }

                            var saleItem = new SaleItem(
                                product: product,
                                quantity: orderItem.Quantity,
                                taxRate: orderItem.TaxRate,
                                isTaxExempt: orderItem.IsTaxExempt,
                                priceOverride: orderItem.UnitPrice // Forzar el precio unitario del pedido
                            );

                            sale.AddItem(saleItem);
                        }

                        sale.MarkAsPaid();
                        await _saleRepository.AddAsync(sale, cancellationToken);

                        order.Settle(request.UserId);
                    }
                    else
                    {
                        var reason = !string.IsNullOrWhiteSpace(settleResult.ReturnReason) 
                            ? settleResult.ReturnReason 
                            : (order.ReturnReason ?? "Devuelto en Liquidación");

                        if (order.Status == OrderStatus.EnRoute)
                        {
                            order.MarkAsReturned(reason);
                        }

                        // Reingresar el stock al almacén
                        var orderWithItems = await _orderRepository.GetByIdWithItemsAsync(order.Id, cancellationToken);
                        if (orderWithItems != null)
                        {
                            foreach (var orderItem in orderWithItems.Items)
                            {
                                var product = await _context.Products.FindAsync(new object[] { orderItem.ProductId }, cancellationToken);
                                if (product != null && product.ControlExistencia != ControlExistencia.SinControl)
                                {
                                    var branchStock = await _context.ProductBranchStocks
                                        .FirstOrDefaultAsync(s => s.ProductId == product.Id && s.BranchId == order.BranchId, cancellationToken);

                                    if (branchStock != null)
                                    {
                                        branchStock.ApplyMovement(orderItem.Quantity, InventoryMovementType.Return, order.Id, $"Devolución de Ruta (Motivo: {reason})");
                                    }
                                }
                            }
                        }

                        order.Settle(request.UserId);
                    }

                    await _orderRepository.UpdateAsync(order, cancellationToken);
                }

                await _routeRepository.UpdateAsync(route, cancellationToken);
                await _context.SaveChangesAsync(cancellationToken);
                await _context.CommitTransactionAsync(cancellationToken);

                return true;
            }
            catch (DbUpdateConcurrencyException) when (attempt < maxRetries)
            {
                await _context.RollbackTransactionAsync(cancellationToken);
                await Task.Delay(50 * attempt, cancellationToken);
                continue;
            }
            catch
            {
                await _context.RollbackTransactionAsync(cancellationToken);
                throw;
            }
        }
    }
}
