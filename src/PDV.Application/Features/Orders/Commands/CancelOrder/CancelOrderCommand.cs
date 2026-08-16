using MediatR;
using Microsoft.EntityFrameworkCore;
using PDV.Application.Common.Interfaces;
using PDV.Application.Common.Security;
using PDV.Domain.Enums;
using PDV.Domain.Exceptions;
using PDV.Domain.Repositories;
using PDV.Domain.Entities;

namespace PDV.Application.Features.Orders.Commands.CancelOrder;

[AuthorizeCommand("orders.cancel")]
public record CancelOrderCommand(
    Guid OrderId,
    string Reason,
    string UserId,
    string? SupervisorUsername = null,
    string? SupervisorPassword = null) : IRequest<bool>, ISupervisorAuthorizedCommand, ISupervisorAuthorizedTarget
{
    public string? AuthorizedByUserId { get; set; }
}
    

public class CancelOrderCommandHandler : IRequestHandler<CancelOrderCommand, bool>
{
    private readonly IOrderRepository _orderRepository;
    private readonly IApplicationDbContext _context;

    public CancelOrderCommandHandler(IOrderRepository orderRepository, IApplicationDbContext context)
    {
        _orderRepository = orderRepository;
        _context = context;
    }

    public async Task<bool> Handle(CancelOrderCommand request, CancellationToken cancellationToken)
    {
        await _context.BeginTransactionAsync(cancellationToken);

        try
        {
            var order = await _orderRepository.GetByIdWithItemsAsync(request.OrderId, cancellationToken);
            if (order == null)
            {
                throw new DomainException("Pedido no encontrado.");
            }

            if (order.Status == OrderStatus.Cancelled)
            {
                throw new DomainException("El pedido ya se encuentra cancelado.");
            }

            if (order.Status == OrderStatus.Delivered)
            {
                throw new DomainException("Un pedido entregado no puede ser cancelado directamente.");
            }

            // Cambiar estado a Cancelled
            order.Cancel(request.Reason);

            // Devolver stock al almacén
            foreach (var item in order.Items)
            {
                var product = await _context.Products.FindAsync(new object[] { item.ProductId }, cancellationToken);
                if (product != null && product.ControlExistencia != ControlExistencia.SinControl)
                {
                    var branchStock = await _context.ProductBranchStocks
                        .FirstOrDefaultAsync(s => s.ProductId == product.Id && s.BranchId == order.BranchId, cancellationToken);

                    if (branchStock != null)
                    {
                        branchStock.ApplyMovement(item.Quantity, InventoryMovementType.Return, order.Id, $"Cancelación de Pedido: {request.Reason}");
                    }
                }
            }

            await _orderRepository.UpdateAsync(order, cancellationToken);
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
