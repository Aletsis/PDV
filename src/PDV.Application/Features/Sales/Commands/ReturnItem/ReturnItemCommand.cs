using MediatR;
using Microsoft.EntityFrameworkCore;
using PDV.Application.Common.Interfaces;
using PDV.Domain.Entities;
using PDV.Domain.Enums;

namespace PDV.Application.Features.Sales.Commands.ReturnItem;

public record ReturnItemCommand(Guid SaleItemId, decimal Quantity, string Reason, string CashierUserId) : IRequest<bool>;

public class ReturnItemCommandHandler : IRequestHandler<ReturnItemCommand, bool>
{
    private readonly IApplicationDbContext _context;

    public ReturnItemCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<bool> Handle(ReturnItemCommand request, CancellationToken cancellationToken)
    {
        var item = await _context.SaleItems.FindAsync(new object[] { request.SaleItemId }, cancellationToken);
        if (item == null) return false;

        var sale = await _context.Sales.FindAsync(new object[] { item.SaleId }, cancellationToken);
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

        // Buscar cantidad ya devuelta en base de datos para este producto en esta venta
        var alreadyReturned = await _context.Returns
            .Where(r => r.SaleId == sale.Id)
            .SelectMany(r => r.Items)
            .Where(ri => ri.ProductId == item.ProductId)
            .SumAsync(ri => ri.Quantity, cancellationToken);

        var remainingQuantity = item.Quantity - alreadyReturned;
        var qtyToReturn = Math.Min(request.Quantity, remainingQuantity);
        if (qtyToReturn <= 0) return false;

        // Buscar turno activo del cajero que realiza la devolución
        var activeShift = await _context.Shifts
            .FirstOrDefaultAsync(s => s.UserId == request.CashierUserId && s.Status == ShiftStatus.Open, cancellationToken);

        if (activeShift == null)
        {
            activeShift = await _context.Shifts
                .FirstOrDefaultAsync(s => s.CashRegisterId == sale.CashRegisterId && s.Status == ShiftStatus.Open, cancellationToken);
        }

        if (activeShift == null)
        {
            throw new InvalidOperationException("No se puede registrar una devolución si no hay un turno de caja activo.");
        }

        // Obtener el producto para incrementar stock
        var product = await _context.Products.FindAsync(new object[] { item.ProductId }, cancellationToken);
        if (product == null) return false;

        var ret = new Return(
            reason: request.Reason,
            refundMethod: RefundMethod.Cash,
            userId: request.CashierUserId,
            shiftId: activeShift.Id,
            series: "R",
            folio: 0,
            saleId: sale.Id,
            clientId: sale.ClientId,
            cashRegisterId: activeShift.CashRegisterId,
            employeeId: null
        );

        var returnItemObj = new PDV.Domain.Entities.ReturnItem(
            product: product,
            quantity: qtyToReturn,
            unitPrice: item.UnitPrice,
            taxRate: item.TaxRate,
            isTaxExempt: item.IsTaxExempt
        );

        ret.AddItem(returnItemObj);
        ret.Complete();

        _context.Returns.Add(ret);

        // Incrementar el inventario (a la inversa de una venta)
        product.IncreaseStock(qtyToReturn);

        // Marcar el ítem como devuelto si ya se devolvió por completo
        if (alreadyReturned + qtyToReturn >= item.Quantity)
        {
            item.MarkAsReturned();
        }

        // Comprobar si todos los items de la venta están devueltos
        var allSaleItems = await _context.SaleItems.Where(si => si.SaleId == sale.Id).ToListAsync(cancellationToken);
        bool allItemsReturned = true;
        foreach (var si in allSaleItems)
        {
            var totalReturnedForProduct = await _context.Returns
                .Where(r => r.SaleId == sale.Id)
                .SelectMany(r => r.Items)
                .Where(ri => ri.ProductId == si.ProductId)
                .SumAsync(ri => ri.Quantity, cancellationToken);
            
            // Si el que se está devolviendo ahora es para este producto, sumarlo
            if (si.ProductId == product.Id)
            {
                totalReturnedForProduct += qtyToReturn;
            }
            
            if (totalReturnedForProduct < si.Quantity)
            {
                allItemsReturned = false;
                break;
            }
        }
        
        if (allItemsReturned)
        {
            sale.MarkAsReturned();
        }

        await _context.SaveChangesAsync(cancellationToken);

        return true;
    }
}
