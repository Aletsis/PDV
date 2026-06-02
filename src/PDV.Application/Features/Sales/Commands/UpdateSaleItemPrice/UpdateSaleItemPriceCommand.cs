using MediatR;
using Microsoft.EntityFrameworkCore;
using PDV.Application.Common.Interfaces;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace PDV.Application.Features.Sales.Commands.UpdateSaleItemPrice;

public record UpdateSaleItemPriceCommand(Guid SaleId, Guid SaleItemId, decimal? NewPriceOverride) : IRequest<bool>;

public class UpdateSaleItemPriceCommandHandler : IRequestHandler<UpdateSaleItemPriceCommand, bool>
{
    private readonly IApplicationDbContext _context;

    public UpdateSaleItemPriceCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<bool> Handle(UpdateSaleItemPriceCommand request, CancellationToken cancellationToken)
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

                var saleItem = await _context.SaleItems
                    .Include(i => i.Product)
                    .FirstOrDefaultAsync(i => i.Id == request.SaleItemId, cancellationToken);

                if (saleItem == null)
                    throw new InvalidOperationException("Artículo no encontrado en la base de datos.");

                // Actualizar precio override en dominio
                sale.UpdateItemPrice(request.SaleItemId, request.NewPriceOverride ?? saleItem.Product.Price);

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
