using MediatR;
using Microsoft.EntityFrameworkCore;
using PDV.Application.Common.Interfaces;
using PDV.Application.Common.Security;

namespace PDV.Application.Features.Orders.Commands.UpdateOrderItemPrice;

[AuthorizeCommand("orders.override_price")]
public record UpdateOrderItemPriceCommand(
    Guid OrderId, 
    Guid OrderItemId, 
    decimal? NewPriceOverride,
    string? SupervisorUsername = null,
    string? SupervisorPassword = null) : IRequest<bool>, ISupervisorAuthorizedCommand;

public class UpdateOrderItemPriceCommandHandler : IRequestHandler<UpdateOrderItemPriceCommand, bool>
{
    private readonly IApplicationDbContext _context;

    public UpdateOrderItemPriceCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<bool> Handle(UpdateOrderItemPriceCommand request, CancellationToken cancellationToken)
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
                var order = await _context.Orders
                    .Include(o => o.Items)
                    .FirstOrDefaultAsync(o => o.Id == request.OrderId, cancellationToken);

                if (order == null)
                    throw new InvalidOperationException("Pedido no encontrado.");

                if (!order.IsEditable)
                    throw new InvalidOperationException("No se pueden modificar artículos de un pedido cerrado o entregado.");


                if (order.IsCancelled)
                    throw new InvalidOperationException("No se pueden modificar artículos de un pedido cancelado.");

                var orderItem = await _context.OrderItems
                    .Include(i => i.Product)
                    .FirstOrDefaultAsync(i => i.Id == request.OrderItemId, cancellationToken);

                if (orderItem == null)
                    throw new InvalidOperationException("Artículo no encontrado en la base de datos.");

                // Actualizar precio override en dominio
                order.UpdateItemPrice(request.OrderItemId, request.NewPriceOverride ?? orderItem.Product.Price);

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