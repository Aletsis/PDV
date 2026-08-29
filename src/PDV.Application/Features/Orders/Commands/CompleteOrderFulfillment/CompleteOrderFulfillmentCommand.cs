using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PDV.Application.Common.Interfaces;
using PDV.Domain.Exceptions;

namespace PDV.Application.Features.Orders.Commands.CompleteOrderFulfillment;

public record CompleteOrderFulfillmentCommand(Guid OrderId, string? UserId = null) : IRequest<bool>;

public class CompleteOrderFulfillmentCommandHandler : IRequestHandler<CompleteOrderFulfillmentCommand, bool>
{
    private readonly IApplicationDbContext _context;
    private readonly IPickerDispatcherService _pickerDispatcher;

    public CompleteOrderFulfillmentCommandHandler(
        IApplicationDbContext context,
        IPickerDispatcherService pickerDispatcher)
    {
        _context = context;
        _pickerDispatcher = pickerDispatcher;
    }

    public async Task<bool> Handle(CompleteOrderFulfillmentCommand request, CancellationToken cancellationToken)
    {
        var order = await _context.Orders
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == request.OrderId, cancellationToken);

        if (order == null)
            throw new DomainException("Pedido no encontrado.");

        string? effectivePickerId = request.UserId ?? order.FilledById;

        order.MarkAsFilled(effectivePickerId);

        // Marcar todos los ítems como surtidos
        foreach (var item in order.Items)
        {
            item.MarkFulfilled(true);
        }

        // Registrar orden completada en el perfil del surtidor si existe
        if (!string.IsNullOrWhiteSpace(effectivePickerId))
        {
            var pickerStatus = await _context.UserWorkStatuses
                .FirstOrDefaultAsync(s => s.UserId == effectivePickerId && s.BranchId == order.BranchId, cancellationToken);

            pickerStatus?.RecordOrderCompleted();
        }

        await _context.SaveChangesAsync(cancellationToken);

        // Auto-asignar siguiente pedido en cola si el surtidor sigue disponible
        if (!string.IsNullOrWhiteSpace(effectivePickerId))
        {
            await _pickerDispatcher.TryAssignNextPendingOrdersToPickerAsync(effectivePickerId, order.BranchId, cancellationToken);
        }

        return true;
    }
}
