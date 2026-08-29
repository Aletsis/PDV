using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PDV.Application.Common.Interfaces;
using PDV.Application.Features.Orders.Dtos;
using PDV.Domain.Enums;

namespace PDV.Application.Features.Orders.Queries.GetOrdersForFulfillment;

public record GetOrdersForFulfillmentQuery : IRequest<List<OrderDetailDto>>
{
    public Guid BranchId { get; set; }
    public string? SearchTerm { get; set; }
    public string? UserId { get; set; }
}

public class GetOrdersForFulfillmentQueryHandler : IRequestHandler<GetOrdersForFulfillmentQuery, List<OrderDetailDto>>
{
    private readonly IApplicationDbContext _context;

    public GetOrdersForFulfillmentQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<OrderDetailDto>> Handle(GetOrdersForFulfillmentQuery request, CancellationToken cancellationToken)
    {
        var query = _context.Orders
            .Include(o => o.Client)
            .Include(o => o.DeliveryZone)
            .Include(o => o.Items)
                .ThenInclude(i => i.Product)
            .Where(o => o.BranchId == request.BranchId &&
                        (o.Status == OrderStatus.Pending || o.Status == OrderStatus.InFulfillment))
            .AsNoTracking();

        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            var term = request.SearchTerm.Trim().ToLower();
            query = query.Where(o =>
                (o.Client != null && o.Client.Name.ToLower().Contains(term)) ||
                (o.Series != null && o.Series.ToLower().Contains(term)) ||
                o.Folio.ToString().Contains(term) ||
                (o.GeneralNotes != null && o.GeneralNotes.ToLower().Contains(term)));
        }

        var orders = await query
            .OrderBy(o => o.OrderDate)
            .ToListAsync(cancellationToken);

        return orders.Select(o => new OrderDetailDto
        {
            Id = o.Id,
            OrderNumber = $"{o.Series ?? "PED"}-{o.Folio}",
            Date = o.OrderDate,
            TotalAmount = o.TotalAmount,
            PaymentMethod = o.PaymentMethod.ToString(),
            ClientId = o.ClientId,
            ClientName = o.Client?.Name ?? "Público General",
            ClientAddress = o.Client?.Address?.ToFullAddressString(),
            ClientPhone = o.Client?.Phone,
            IsCancelled = o.IsCancelled,
            Status = o.Status,
            Series = o.Series,
            Folio = o.Folio,
            ShiftId = o.ShiftId,
            CashRegisterId = o.CashRegisterId,
            DeliveryZoneId = o.DeliveryZoneId,
            DeliveryZoneName = o.DeliveryZone?.Name,
            TakenById = o.TakenById,
            FilledById = o.FilledById,
            VerifiedById = o.VerifiedById,
            GeneralNotes = o.GeneralNotes,
            DeliveryNotes = o.DeliveryNotes,
            IsOutOfZone = o.IsOutOfZone,
            FulfillmentStartedAt = o.FulfillmentStartedAt,
            FilledAt = o.FilledAt,
            Items = o.Items.Select(i => new OrderItemDto
            {
                Id = i.Id,
                ProductId = i.ProductId,
                ProductName = i.ProductName,
                UnitPrice = i.UnitPrice,
                Quantity = i.Quantity,
                RequestedQuantity = i.RequestedQuantity,
                TotalPrice = i.TotalAmount,
                Notes = i.Notes,
                IsFulfilled = i.IsFulfilled
            }).ToList()
        }).ToList();
    }
}
