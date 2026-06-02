using MediatR;
using Microsoft.EntityFrameworkCore;
using PDV.Application.Common.Interfaces;
using PDV.Domain.Entities;
using PDV.Domain.Enums;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace PDV.Application.Features.Sales.Commands.RemoveSaleItem;

public record RemoveSaleItemCommand(Guid SaleId, Guid SaleItemId) : IRequest<bool>;

public class RemoveSaleItemCommandHandler : IRequestHandler<RemoveSaleItemCommand, bool>
{
    private readonly IApplicationDbContext _context;

    public RemoveSaleItemCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<bool> Handle(RemoveSaleItemCommand request, CancellationToken cancellationToken)
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
                    throw new InvalidOperationException("No se pueden remover artículos de una venta pagada.");

                if (sale.IsCancelled)
                    throw new InvalidOperationException("No se pueden remover artículos de una venta cancelada.");

                var saleItem = await _context.SaleItems.FindAsync(new object[] { request.SaleItemId }, cancellationToken);
                if (saleItem == null)
                    throw new InvalidOperationException("Artículo no encontrado en la base de datos.");

                var product = await _context.Products.FindAsync(new object[] { saleItem.ProductId }, cancellationToken);
                if (product != null)
                {
                    // Reintegrar stock (Kardex)
                    product.ApplyMovement(saleItem.Quantity, InventoryMovementType.Sale, sale.Id, "Reversión por artículo removido en POS");
                }

                // Registrar cancelación parcial para auditoría en el servidor
                var cancellation = new Cancellation(
                    branchId: sale.BranchId,
                    type: CancellationType.Product,
                    reason: $"Cancelación Parcial POS - Código: {product?.Code ?? "N/A"} | Nombre: {product?.Name ?? "Desconocido"} | Cantidad: {saleItem.Quantity} | Total Revertido: ${(saleItem.Quantity * (saleItem.PriceOverride ?? saleItem.UnitPrice)).ToString("F2")} | Razón: Eliminado del carrito",
                    userId: sale.UserId ?? "Anonymous",
                    saleId: sale.Id,
                    saleItemId: saleItem.Id
                );
                _context.Cancellations.Add(cancellation);

                // Remover de la colección en memoria para que se recalculen totales en dominio
                sale.RemoveItem(request.SaleItemId);

                // Remover físicamente de la base de datos
                _context.SaleItems.Remove(saleItem);

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
