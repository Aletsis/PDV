using MediatR;
using Microsoft.EntityFrameworkCore;
using PDV.Application.Common.Interfaces;
using PDV.Application.Common.Security;
using PDV.Domain.Enums;

namespace PDV.Application.Features.Orders.Commands.UpdateOrderItemQuantity;

[AuthorizeCommand("orders.cancel_item")]
public record UpdateOrderItemQuantityCommand(
    Guid OrderId, 
    Guid OrderItemId, 
    decimal NewQuantity,
    string? SupervisorUsername = null,
    string? SupervisorPassword = null
) : IRequest<bool>, ISupervisorAuthorizedCommand, ISupervisorAuthorizedTarget
{
    public string? AuthorizedByUserId { get; set; }
}

public class UpdateOrderItemQuantityCommandHandler : IRequestHandler<UpdateOrderItemQuantityCommand, bool>
{
    private readonly IApplicationDbContext _context;

    public UpdateOrderItemQuantityCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<bool> Handle(UpdateOrderItemQuantityCommand request, CancellationToken cancellationToken)
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
                    throw new InvalidOperationException("Pedido no encontrada.");

                if (!order.IsEditable)
                    throw new InvalidOperationException("No se pueden modificar artículos de un pedido cerrado o entregado.");

                if (order.IsCancelled)
                    throw new InvalidOperationException("No se pueden modificar artículos de un pedido cancelado.");

                var orderItem = await _context.OrderItems.FindAsync(new object[] { request.OrderItemId }, cancellationToken);
                if (orderItem == null)
                    throw new InvalidOperationException("Artículo no encontrado en la base de datos.");

                var product = await _context.Products.FindAsync(new object[] { orderItem.ProductId }, cancellationToken);
                if (product == null)
                    throw new InvalidOperationException("Producto no encontrado.");

                // Calcular delta (diferencia)
                decimal delta = request.NewQuantity - orderItem.Quantity;

                var branchStock = await _context.ProductBranchStocks
                    .FirstOrDefaultAsync(s => s.ProductId == orderItem.ProductId && s.BranchId == order.BranchId, cancellationToken);
                
                if (product.ControlExistencia != ControlExistencia.SinControl)
                {
                    if (branchStock == null)
                    {
                        throw new InvalidOperationException(
                            $"No se encontró inventario configurado para el producto {product.Name} en esta sucursal.");
                    }

                    if (delta > 0)
                    {
                        // Validar si hay stock disponible para el incremento
                        if (!branchStock.HasStock(delta))
                        {
                            throw new InvalidOperationException(
                                $"Stock insuficiente para el incremento del producto {product.Name} en esta sucursal. Disponible: {branchStock.Stock}, Requerido: {delta}");
                        }
                    }
                }

                // Aplicar movimiento de stock proporcional (Kardex)
                if (delta != 0 && product.ControlExistencia != ControlExistencia.SinControl && branchStock != null)
                {
                    branchStock.ApplyMovement(-delta, InventoryMovementType.Sale, order.Id, $"Ajuste de cantidad a {request.NewQuantity} piezas");
                }

                // Actualizar cantidad e importes en dominio
                order.UpdateItemQuantity(request.OrderItemId, request.NewQuantity);

                await _context.SaveChangesAsync(cancellationToken);
                await _context.CommitTransactionAsync(cancellationToken);

                return true;
            }
            catch (DbUpdateConcurrencyException ex)
            {
                await _context.RollbackTransactionAsync(cancellationToken);
                if (attempt < maxRetries)
                {
                    await Task.Delay(50 * attempt, cancellationToken);
                    continue;
                }

                var msg = new System.Text.StringBuilder("Error de concurrencia en BD: ");
                foreach (var entry in ex.Entries)
                {
                    var entityName = entry.Entity.GetType().Name;
                    var databaseValues = await entry.GetDatabaseValuesAsync(cancellationToken);
                    var idProp = entry.Properties.FirstOrDefault(p => p.Metadata.Name == "Id")?.CurrentValue;

                    msg.Append($"[Entidad: {entityName}, ID: {idProp}, Estado: {entry.State}]");

                    if (databaseValues == null)
                    {
                        msg.Append(" - El registro ya no existe en la base de datos.");
                    }
                    else
                    {
                        var rowVersionProp = entry.Properties.FirstOrDefault(p => p.Metadata.Name == "RowVersion");
                        if (rowVersionProp != null)
                        {
                            var loadedVal = rowVersionProp.OriginalValue is byte[] bLoaded ? Convert.ToBase64String(bLoaded) : rowVersionProp.OriginalValue?.ToString();
                            var currentVal = rowVersionProp.CurrentValue is byte[] bCurrent ? Convert.ToBase64String(bCurrent) : rowVersionProp.CurrentValue?.ToString();

                            var dbValObj = databaseValues["RowVersion"];
                            var dbVal = dbValObj is byte[] bDb ? Convert.ToBase64String(bDb) : dbValObj?.ToString();

                            msg.Append($" - RowVersion original cargada: {loadedVal}, RowVersion actual en memoria: {currentVal}, RowVersion en base de datos: {dbVal}");
                        }
                    }
                }

                Console.WriteLine(msg.ToString());
                throw new InvalidOperationException(msg.ToString(), ex);
            }
            catch (Exception)
            {
                await _context.RollbackTransactionAsync(cancellationToken);
                throw;
            }
        }
    }
}