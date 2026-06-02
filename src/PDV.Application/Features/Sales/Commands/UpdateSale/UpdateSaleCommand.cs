using MediatR;
using PDV.Application.Common.Interfaces;
using PDV.Application.Features.Sales.Dtos;
using PDV.Domain.Entities;
using PDV.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using PDV.Domain.Enums;

namespace PDV.Application.Features.Sales.Commands.UpdateSale;

public record UpdateSaleCommand : IRequest<Guid>
{
    public Guid SaleId { get; set; }
    public List<CartItemDto> Items { get; set; } = new();
    public Guid? ClientId { get; set; }
}

public class UpdateSaleCommandHandler : IRequestHandler<UpdateSaleCommand, Guid>
{
    private readonly IApplicationDbContext _context;

    public UpdateSaleCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Guid> Handle(UpdateSaleCommand request, CancellationToken cancellationToken)
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
                {
                    throw new InvalidOperationException("Venta no encontrada.");
                }

                if (sale.IsPaid)
                {
                    throw new InvalidOperationException("No se puede modificar una venta que ya ha sido pagada.");
                }

                if (sale.IsCancelled)
                {
                    throw new InvalidOperationException("No se puede modificar una venta cancelada.");
                }

                // 1. Revertir temporalmente el stock de los productos anteriores
                foreach (var item in sale.Items)
                {
                    var product = await _context.Products.FindAsync(new object[] { item.ProductId }, cancellationToken);
                    if (product != null)
                    {
                        product.IncreaseStock(item.Quantity);
                    }
                }

                // 2. Eliminar los items antiguos de la venta y del contexto
                var oldItems = sale.Items.ToList();
                sale.LoadItems(new List<SaleItem>());
                _context.SaleItems.RemoveRange(oldItems);

                // 3. Crear los nuevos items, validar stock y aplicar reducción
                var newItems = new List<SaleItem>();
                foreach (var item in request.Items)
                {
                    var product = await _context.Products.FindAsync(new object[] { item.Product.Id }, cancellationToken);
                    if (product == null)
                        throw new InvalidOperationException($"Producto {item.Product.Name} no encontrado.");

                    if (!product.IsActive)
                        throw new InvalidOperationException($"El producto {product.Name} está inactivo.");

                    decimal quantity = product.SaleType == SaleType.Bulk ? item.Weight : item.Quantity;

                    if (!product.HasStock(quantity))
                    {
                        throw new InvalidOperationException(
                            $"Stock insuficiente para el producto {product.Name}. Disponible: {product.Stock}, Requerido: {quantity}");
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

                    var saleItem = new SaleItem(
                        product: product,
                        quantity: quantity,
                        taxRate: taxRatePercent,
                        isTaxExempt: isExempt,
                        priceOverride: item.PriceOverride);

                    newItems.Add(saleItem);

                    // Aplicar movimiento de inventario transaccional (Kardex)
                    product.ApplyMovement(-quantity, InventoryMovementType.Sale, sale.Id);
                }

                // 4. Cargar los nuevos items en la venta (esto recalcula subtotales e impuestos en el dominio)
                sale.LoadItems(newItems);

                // 5. Actualizar el cliente si ha cambiado
                sale.SetClient(request.ClientId);

                await _context.SaveChangesAsync(cancellationToken);
                await _context.CommitTransactionAsync(cancellationToken);

                return sale.Id;
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
