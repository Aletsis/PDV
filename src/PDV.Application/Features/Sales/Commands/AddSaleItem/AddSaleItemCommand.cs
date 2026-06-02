using MediatR;
using Microsoft.EntityFrameworkCore;
using PDV.Application.Common.Interfaces;
using PDV.Domain.Entities;
using PDV.Domain.Enums;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace PDV.Application.Features.Sales.Commands.AddSaleItem;

public record AddSaleItemCommand : IRequest<Guid>
{
    public Guid SaleId { get; set; }
    public Guid ProductId { get; set; }
    public decimal Quantity { get; set; }
    public decimal? PriceOverride { get; set; }
}

public class AddSaleItemCommandHandler : IRequestHandler<AddSaleItemCommand, Guid>
{
    private readonly IApplicationDbContext _context;

    public AddSaleItemCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Guid> Handle(AddSaleItemCommand request, CancellationToken cancellationToken)
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
                    throw new InvalidOperationException("No se pueden agregar artículos a una venta pagada.");

                if (sale.IsCancelled)
                    throw new InvalidOperationException("No se pueden agregar artículos a una venta cancelada.");

                var product = await _context.Products.FindAsync(new object[] { request.ProductId }, cancellationToken);
                if (product == null)
                    throw new InvalidOperationException("Producto no encontrado.");

                if (!product.IsActive)
                    throw new InvalidOperationException($"El producto {product.Name} está inactivo.");

                if (!product.HasStock(request.Quantity))
                {
                    throw new InvalidOperationException(
                        $"Stock insuficiente para el producto {product.Name}. Disponible: {product.Stock}, Requerido: {request.Quantity}");
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
                    quantity: request.Quantity,
                    taxRate: taxRatePercent,
                    isTaxExempt: isExempt,
                    priceOverride: request.PriceOverride);

                sale.AddItem(saleItem);
                _context.SaleItems.Add(saleItem);

                // Registrar movimiento de inventario transaccional (Kardex)
                product.ApplyMovement(-request.Quantity, InventoryMovementType.Sale, sale.Id);

                await _context.SaveChangesAsync(cancellationToken);
                await _context.CommitTransactionAsync(cancellationToken);

                return saleItem.Id;
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
