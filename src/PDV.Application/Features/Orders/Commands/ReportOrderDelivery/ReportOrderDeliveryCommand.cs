using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PDV.Application.Common.Interfaces;
using PDV.Domain.Exceptions;

namespace PDV.Application.Features.Orders.Commands.ReportOrderDelivery;

public record ReportOrderDeliveryCommand : IRequest<bool>
{
    public Guid OrderId { get; set; }
    public bool IsDelivered { get; set; }
    public string? ReturnReason { get; set; }
    public string? DeliveryManId { get; set; }
}

public class ReportOrderDeliveryCommandHandler : IRequestHandler<ReportOrderDeliveryCommand, bool>
{
    private readonly IApplicationDbContext _context;

    public ReportOrderDeliveryCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<bool> Handle(ReportOrderDeliveryCommand request, CancellationToken cancellationToken)
    {
        var order = await _context.Orders
            .FirstOrDefaultAsync(o => o.Id == request.OrderId, cancellationToken);

        if (order == null)
            throw new DomainException("Pedido no encontrado.");

        if (request.IsDelivered)
        {
            order.MarkAsDelivered();
        }
        else
        {
            if (string.IsNullOrWhiteSpace(request.ReturnReason))
                throw new DomainException("Debe especificar el motivo de no entrega.");

            order.MarkAsReturned(request.ReturnReason);
        }

        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }
}
