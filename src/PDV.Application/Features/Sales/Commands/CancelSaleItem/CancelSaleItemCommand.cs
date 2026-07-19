using MediatR;
using Microsoft.EntityFrameworkCore;
using PDV.Application.Common.Interfaces;
using PDV.Application.Common.Security;
using PDV.Domain.Entities;
using PDV.Domain.Enums;

[AuthorizeCommand("sales.cancel_item")]
public record CancelSaleItemCommand(
    Guid SaleItemId, 
    string Reason, 
    string UserId,
    string? SupervisorUsername = null,
    string? SupervisorPassword = null) : IRequest<bool>, ISupervisorAuthorizedCommand, ISupervisorAuthorizedTarget
{
    public string? AuthorizedByUserId { get; set; }
}

public class CancelSaleItemCommandHandler : IRequestHandler<CancelSaleItemCommand, bool>
{
    private readonly IApplicationDbContext _context;

    public CancelSaleItemCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<bool> Handle(CancelSaleItemCommand request, CancellationToken cancellationToken)
    {
        var item = await _context.SaleItems.FindAsync(new object[] { request.SaleItemId }, cancellationToken);
        if (item == null) return false;

        var sale = await _context.Sales.FindAsync(new object[] { item.SaleId }, cancellationToken);
        if (sale == null) return false;
        
        // Validar que solo se puede cancelar item si la venta no está pagada
        if (sale.IsPaid)
        {
            throw new InvalidOperationException("No se puede cancelar un item de una venta que ya ha sido pagada. Use devolución en su lugar.");
        }

        // Obtener stock de la sucursal para incrementar
        var branchStock = await _context.ProductBranchStocks
            .FirstOrDefaultAsync(s => s.ProductId == item.ProductId && s.BranchId == sale.BranchId, cancellationToken);

        // create cancellation record
        var cancellation = new Cancellation(
            branchId: sale.BranchId,
            type: CancellationType.Product,
            reason: request.Reason,
            userId: request.AuthorizedByUserId ?? request.UserId,
            saleId: sale.Id,
            saleItemId: item.Id
        );

        _context.Cancellations.Add(cancellation);

        // Incrementar stock del producto
        if (branchStock != null)
        {
            branchStock.IncreaseStock(item.Quantity);
        }

        // Usar método de dominio para remover item (esto recalcula el total automáticamente)
        sale.RemoveItem(item.Id);

        await _context.SaveChangesAsync(cancellationToken);

        return true;
    }
}
