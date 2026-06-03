using MediatR;
using Microsoft.EntityFrameworkCore;
using PDV.Application.Common.Interfaces;
using PDV.Domain.Entities;
using PDV.Domain.Enums;

namespace PDV.Application.Features.Sales.Commands.CancelSale;

public record CancelSaleCommand(Guid SaleId, string Reason, string UserId) : IRequest<bool>;

public class CancelSaleCommandHandler : IRequestHandler<CancelSaleCommand, bool>
{
    private readonly IApplicationDbContext _context;

    public CancelSaleCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<bool> Handle(CancelSaleCommand request, CancellationToken cancellationToken)
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
                
                if (sale == null) return false;
                
                // Validar que solo se puede cancelar si no está pagada
                if (sale.IsPaid)
                {
                    throw new InvalidOperationException("No se puede cancelar una venta que ya ha sido pagada. Use devolución en su lugar.");
                }

                // Usar método de dominio para cancelar
                sale.Cancel(request.Reason);

                // Incrementar stock de todos los productos de la venta cancelada
                foreach (var item in sale.Items)
                {
                    var product = await _context.Products.FindAsync(new object[] { item.ProductId }, cancellationToken);
                    if (product != null)
                    {
                        product.IncreaseStock(item.Quantity);
                    }
                }

                var cancellation = new Cancellation(
                    branchId: sale.BranchId,
                    type: CancellationType.Sale,
                    reason: request.Reason,
                    userId: request.UserId,
                    saleId: sale.Id,
                    saleItemId: null
                );

                _context.Cancellations.Add(cancellation);
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
