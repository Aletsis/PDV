using MediatR;
using Microsoft.EntityFrameworkCore;
using PDV.Application.Common.Interfaces;
using PDV.Domain.Entities;
using PDV.Domain.Enums;

namespace PDV.Application.Features.Orders.Commands.AddOrderItem;

public record AddOrderItemCommand : IRequest<Guid>
{
    public Guid OrderId { get; set; }
    public Guid ProductId { get; set; }
    public decimal Quantity { get; set; }
    public decimal? PriceOverride { get; set; }
    public string? Notes { get; set; }
    public decimal? RequestedQuantity { get; set; }
}

public class AddOrderItemCommandHandler : IRequestHandler<AddOrderItemCommand, Guid>
{
    private readonly IApplicationDbContext _context;

    public AddOrderItemCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Guid> Handle(AddOrderItemCommand request, CancellationToken cancellationToken)
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
                    throw new InvalidOperationException("No se pueden agregar artículos a un pedido cerrado o entregado.");

                if (order.IsCancelled)
                    throw new InvalidOperationException("No se pueden agregar artículos a un pedido cancelado.");

                var product = await _context.Products.FindAsync(new object[] { request.ProductId }, cancellationToken);
                if (product == null)
                    throw new InvalidOperationException("Producto no encontrado.");

                if (!product.IsActive)
                    throw new InvalidOperationException($"El producto {product.Name} está inactivo.");

                var branchStock = await _context.ProductBranchStocks
                    .FirstOrDefaultAsync(s => s.ProductId == product.Id && s.BranchId == order.BranchId, cancellationToken);
                
                if (product.ControlExistencia != ControlExistencia.SinControl)
                {
                    if (branchStock == null)
                    {
                        throw new InvalidOperationException(
                            $"No se encontró inventario configurado para el producto {product.Name} en esta sucursal.");
                    }

                    if (!branchStock.HasStock(request.Quantity))
                    {
                        throw new InvalidOperationException(
                            $"Stock insuficiente para el producto {product.Name} en esta sucursal. Disponible: {branchStock.Stock}, Requerido: {request.Quantity}");
                    }
                }

                decimal taxRatePercent = 0m;
                bool isExempt = false;

                switch (product.TaxRate)
                {
                    case TaxRateType.Exempt:
                        isExempt = true;
                        break;
                    case TaxRateType.ZeroRate:
                        taxRatePercent = 0m;
                        break;
                    case TaxRateType.Rate8:
                        taxRatePercent = 8m;
                        break;
                    case TaxRateType.Rate16:
                        taxRatePercent = 16m;
                        break;
                }

                var orderItem = new OrderItem(
                    product: product,
                    quantity: request.Quantity,
                    unitPrice: request.PriceOverride ?? product.Price,
                    taxRate: taxRatePercent,
                    isTaxExempt: isExempt,
                    notes: request.Notes,
                    requestedQuantity: request.RequestedQuantity > 0 ? request.RequestedQuantity : request.Quantity
                );

                order.AddItem(orderItem);
                _context.OrderItems.Add(orderItem);

                // Registrar movimiento de inventario transaccional (Kardex)
                if (product.ControlExistencia != ControlExistencia.SinControl && branchStock != null)
                {
                    branchStock.ApplyMovement(-request.Quantity, InventoryMovementType.Sale, order.Id);
                }

                await _context.SaveChangesAsync(cancellationToken);
                await _context.CommitTransactionAsync(cancellationToken);

                return orderItem.Id;
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

                    foreach (var prop in entry.Properties)
                    {
                        if (prop.IsModified)
                        {
                            msg.Append($" - Propiedad {prop.Metadata.Name}: Original: {prop.OriginalValue}, Actual: {prop.CurrentValue}");
                        }
                    }

                    if (databaseValues == null)
                    {
                        msg.Append(" - El registro ya no existe en la base de datos.");
                    }
                    else
                    {
                        msg.Append(" - Valores en DB: ");
                        foreach (var prop in databaseValues.Properties)
                        {
                            msg.Append($"{prop.Name}: {databaseValues[prop]} | ");
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