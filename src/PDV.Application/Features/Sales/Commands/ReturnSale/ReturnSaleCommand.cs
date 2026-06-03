using MediatR;
using Microsoft.EntityFrameworkCore;
using PDV.Application.Common.Interfaces;
using PDV.Domain.Entities;
using PDV.Domain.Repositories;

namespace PDV.Application.Features.Sales.Commands.ReturnSale;

public record ReturnSaleCommand(Guid SaleId, string Reason, string CashierUserId) : IRequest<bool>;

public class ReturnSaleCommandHandler : IRequestHandler<ReturnSaleCommand, bool>
{
    private readonly ISaleRepository _saleRepository;
    private readonly IApplicationDbContext _context;

    public ReturnSaleCommandHandler(
        ISaleRepository saleRepository,
        IApplicationDbContext context)
    {
        _saleRepository = saleRepository;
        _context = context;
    }

    public async Task<bool> Handle(ReturnSaleCommand request, CancellationToken cancellationToken)
    {
        var sale = await _saleRepository.GetByIdWithItemsAsync(request.SaleId, cancellationToken);
        if (sale == null) return false;

        // Validar que la venta esté pagada para poder hacer devolución
        if (!sale.IsPaid)
        {
            throw new InvalidOperationException("No se puede hacer devolución de una venta que aún no ha sido pagada. Use cancelación en su lugar.");
        }

        // Validar que la venta no esté cancelada
        if (sale.IsCancelled)
        {
            throw new InvalidOperationException("No se puede hacer devolución de una venta cancelada.");
        }

        // Buscar turno activo del cajero que realiza la devolución
        var activeShift = await _context.Shifts
            .FirstOrDefaultAsync(s => s.UserId == request.CashierUserId && s.Status == PDV.Domain.Enums.ShiftStatus.Open, cancellationToken);

        if (activeShift == null)
        {
            activeShift = await _context.Shifts
                .FirstOrDefaultAsync(s => s.CashRegisterId == sale.CashRegisterId && s.Status == PDV.Domain.Enums.ShiftStatus.Open, cancellationToken);
        }

        if (activeShift == null)
        {
            throw new InvalidOperationException("No se puede registrar una devolución si no hay un turno de caja activo.");
        }

        // Crear registro de devolución de venta completa
        var ret = new Return(
            request.Reason,
            PDV.Domain.Enums.RefundMethod.Cash,
            request.CashierUserId,
            activeShift.Id,
            null,
            0,
            sale.Id,
            sale.ClientId,
            activeShift.CashRegisterId
        );

        // Incrementar stock de productos devueltos y agregar items a la devolución
        foreach (var item in sale.Items)
        {
            var product = await _context.Products.FindAsync(new object[] { item.ProductId }, cancellationToken);
            if (product != null)
            {
                var returnItem = new PDV.Domain.Entities.ReturnItem(
                    product,
                    item.Quantity,
                    item.UnitPrice,
                    item.TaxRate,
                    item.IsTaxExempt
                );
                
                ret.AddItem(returnItem);
                product.IncreaseStock(item.Quantity);
            }
        }

        ret.Complete();
        _context.Returns.Add(ret);

        // Marcar la venta como cancelada después de la devolución
        sale.Cancel($"Devolución total: {request.Reason}");

        await _context.SaveChangesAsync(cancellationToken);

        return true;
    }
}
