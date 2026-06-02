using MediatR;
using Microsoft.EntityFrameworkCore;
using PDV.Application.Common.Interfaces;
using PDV.Domain.Enums;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace PDV.Application.Features.Sales.Commands.UpdateSaleItemQuantity;

public record UpdateSaleItemQuantityCommand(Guid SaleId, Guid SaleItemId, decimal NewQuantity) : IRequest<bool>;

public class UpdateSaleItemQuantityCommandHandler : IRequestHandler<UpdateSaleItemQuantityCommand, bool>
{
    private readonly IApplicationDbContext _context;

    public UpdateSaleItemQuantityCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<bool> Handle(UpdateSaleItemQuantityCommand request, CancellationToken cancellationToken)
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
                var sale = await _context.Sales
                    .Include(s => s.Items)
                    .FirstOrDefaultAsync(s => s.Id == request.SaleId, cancellationToken);

                if (sale == null)
                    throw new InvalidOperationException("Venta no encontrada.");

                if (sale.IsPaid)
                    throw new InvalidOperationException("No se pueden modificar artículos de una venta pagada.");

                if (sale.IsCancelled)
                    throw new InvalidOperationException("No se pueden modificar artículos de una venta cancelada.");

                var saleItem = await _context.SaleItems.FindAsync(new object[] { request.SaleItemId }, cancellationToken);
                if (saleItem == null)
                    throw new InvalidOperationException("Artículo no encontrado en la base de datos.");

                var product = await _context.Products.FindAsync(new object[] { saleItem.ProductId }, cancellationToken);
                if (product == null)
                    throw new InvalidOperationException("Producto no encontrado.");

                // Calcular delta (diferencia)
                decimal delta = request.NewQuantity - saleItem.Quantity;

                if (delta > 0)
                {
                    // Validar si hay stock disponible para el incremento
                    if (!product.HasStock(delta))
                    {
                        throw new InvalidOperationException(
                            $"Stock insuficiente para el incremento del producto {product.Name}. Disponible: {product.Stock}, Requerido: {delta}");
                    }
                }

                // Aplicar movimiento de stock proporcional (Kardex)
                if (delta != 0)
                {
                    product.ApplyMovement(-delta, InventoryMovementType.Sale, sale.Id, $"Ajuste de cantidad a {request.NewQuantity} piezas");
                }

                // Actualizar cantidad e importes en dominio
                sale.UpdateItemQuantity(request.SaleItemId, request.NewQuantity);

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
