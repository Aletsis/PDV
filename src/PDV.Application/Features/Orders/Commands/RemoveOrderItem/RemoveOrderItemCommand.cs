using MediatR;
using Microsoft.EntityFrameworkCore;
using PDV.Application.Common.Interfaces;
using PDV.Domain.Enums;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace PDV.Application.Features.Orders.Commands.RemoveOrderItem;

public record RemoveOrderItemCommand : IRequest<bool>
{
    public Guid OrderId { get; init; }
    public Guid ProductId { get; init; }
}

public class RemoveOrderItemCommandHandler : IRequestHandler<RemoveOrderItemCommand, bool>
{
    private readonly IApplicationDbContext _context;

    public RemoveOrderItemCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<bool> Handle(RemoveOrderItemCommand request, CancellationToken cancellationToken)
    {
        await _context.BeginTransactionAsync(cancellationToken);

        try
        {
            var order = await _context.Orders
                .Include(o => o.Items)
                .FirstOrDefaultAsync(o => o.Id == request.OrderId, cancellationToken);

            if (order == null)
                throw new InvalidOperationException("Pedido no encontrado.");

            if (!order.IsEditable)
                throw new InvalidOperationException("No se pueden remover artículos de un pedido cerrado.");

            if (order.IsCancelled)
                throw new InvalidOperationException("No se pueden remover artículos de un pedido cancelado.");

            var item = order.Items.FirstOrDefault(i => i.ProductId == request.ProductId);
            if (item == null)
                throw new InvalidOperationException("El artículo no se encuentra en el pedido.");

            // Devolver stock preventivo al almacén
            var product = await _context.Products.FindAsync(new object[] { item.ProductId }, cancellationToken);
            if (product != null && product.ControlExistencia != ControlExistencia.SinControl)
            {
                var branchStock = await _context.ProductBranchStocks
                    .FirstOrDefaultAsync(s => s.ProductId == product.Id && s.BranchId == order.BranchId, cancellationToken);

                if (branchStock != null)
                {
                    branchStock.ApplyMovement(item.Quantity, InventoryMovementType.Return, order.Id, "Artículo removido del pedido");
                }
            }

            // Remover de la entidad de dominio y del DbContext
            order.RemoveItem(request.ProductId);
            _context.OrderItems.Remove(item);

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
