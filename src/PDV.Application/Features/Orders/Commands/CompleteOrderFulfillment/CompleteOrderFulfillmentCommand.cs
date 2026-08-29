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

    public CompleteOrderFulfillmentCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<bool> Handle(CompleteOrderFulfillmentCommand request, CancellationToken cancellationToken)
    {
        var order = await _context.Orders
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == request.OrderId, cancellationToken);

        if (order == null)
            throw new DomainException("Pedido no encontrado.");

        order.MarkAsFilled(request.UserId);

        // Marcar todos los ítems como surtidos
        foreach (var item in order.Items)
        {
            item.MarkFulfilled(true);
        }

        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }
}
