using MediatR;
using PDV.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;
using PDV.Domain.Enums;

namespace PDV.Application.Features.Sales.Commands.FinalizeSale;

public record FinalizeSaleCommand : IRequest<Guid>
{
    public Guid SaleId { get; set; }
    public string PaymentMethod { get; set; } = "Cash";
    public Guid? ClientId { get; set; }
    public bool RequiresInvoice { get; set; } = false;
}

public class FinalizeSaleCommandHandler : IRequestHandler<FinalizeSaleCommand, Guid>
{
    private readonly IApplicationDbContext _context;

    public FinalizeSaleCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Guid> Handle(FinalizeSaleCommand request, CancellationToken cancellationToken)
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
                    throw new InvalidOperationException("La venta ya se encuentra pagada.");
                }

                if (sale.IsCancelled)
                {
                    throw new InvalidOperationException("La venta se encuentra cancelada.");
                }

                // Actualizar cliente final si cambió
                sale.SetClient(request.ClientId);

                // Actualizar método de pago definitivo
                var paymentMethod = Enum.TryParse<PaymentMethodType>(request.PaymentMethod, true, out var pm) ? pm : PaymentMethodType.Cash;
                sale.SetPaymentMethod(paymentMethod);

                // Marcar la venta como pagada (esto dispara el eventoPaymentMadeEvent)
                sale.MarkAsPaid();

                // Validar requerimiento de factura
                if (request.RequiresInvoice)
                {
                    if (!request.ClientId.HasValue)
                    {
                        throw new InvalidOperationException("Para facturar se requiere seleccionar un cliente con datos fiscales completos.");
                    }

                    sale.RequestInvoice();
                }

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
