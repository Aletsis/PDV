using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PDV.Application.Common.Interfaces;
using PDV.Domain.Exceptions;

namespace PDV.Application.Features.Orders.Commands.StartOrderFulfillment;

public record StartOrderFulfillmentCommand(Guid OrderId, string UserId) : IRequest<bool>;

public class StartOrderFulfillmentCommandHandler : IRequestHandler<StartOrderFulfillmentCommand, bool>
{
    private readonly IApplicationDbContext _context;

    public StartOrderFulfillmentCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<bool> Handle(StartOrderFulfillmentCommand request, CancellationToken cancellationToken)
    {
        var order = await _context.Orders
            .FirstOrDefaultAsync(o => o.Id == request.OrderId, cancellationToken);

        if (order == null)
            throw new DomainException("Pedido no encontrado.");

        order.AssignPicker(request.UserId);

        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }
}
