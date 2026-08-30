using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PDV.Application.Common.Interfaces;
using PDV.Application.Features.Orders.Dtos;
using PDV.Domain.Entities;
using PDV.Domain.Enums;

namespace PDV.Application.Features.Orders.Queries.GetOrderMonitoring;

public record GetOrderMonitoringQuery : IRequest<OrderMonitoringResultDto>
{
    public Guid BranchId { get; init; }
    public DateTime? StartDate { get; init; }
    public DateTime? EndDate { get; init; }
    public OrderStatus? Status { get; init; }
    public bool OnlyActive { get; init; } = true;
    public bool OnlyDelayed { get; init; } = false;
    public string? SearchTerm { get; init; }
    public Guid? DeliveryZoneId { get; init; }
    public string? AssignedUserId { get; init; }
}

public class GetOrderMonitoringQueryHandler : IRequestHandler<GetOrderMonitoringQuery, OrderMonitoringResultDto>
{
    private readonly IApplicationDbContext _context;
    private readonly IIdentityService _identityService;

    public GetOrderMonitoringQueryHandler(
        IApplicationDbContext context,
        IIdentityService identityService)
    {
        _context = context;
        _identityService = identityService;
    }

    public async Task<OrderMonitoringResultDto> Handle(GetOrderMonitoringQuery request, CancellationToken cancellationToken)
    {
        var now = DateTime.Now;
        var todayStart = DateTime.Today;

        var query = _context.Orders
            .Include(o => o.Client)
            .Include(o => o.DeliveryZone)
            .Include(o => o.DeliveryRoute)
            .Include(o => o.Items)
                .ThenInclude(i => i.Product)
            .Where(o => o.BranchId == request.BranchId)
            .AsNoTracking();

        // 1. Filtrado por fechas o activos
        if (request.StartDate.HasValue || request.EndDate.HasValue)
        {
            if (request.StartDate.HasValue)
            {
                var s = request.StartDate.Value.Date;
                query = query.Where(o => o.OrderDate >= s);
            }
            if (request.EndDate.HasValue)
            {
                var e = request.EndDate.Value.Date.AddDays(1).AddTicks(-1);
                query = query.Where(o => o.OrderDate <= e);
            }
        }
        else if (request.OnlyActive)
        {
            // Pedidos activos O pedidos creados/modificados en las últimas 24 horas
            var last24h = now.AddHours(-24);
            query = query.Where(o =>
                (o.Status != OrderStatus.Delivered && o.Status != OrderStatus.Settled && o.Status != OrderStatus.Cancelled) ||
                o.OrderDate >= last24h ||
                (o.DeliveredAt.HasValue && o.DeliveredAt.Value >= last24h) ||
                (o.SettledAt.HasValue && o.SettledAt.Value >= last24h));
        }

        // 2. Filtrado por estado explícito
        if (request.Status.HasValue)
        {
            query = query.Where(o => o.Status == request.Status.Value);
        }

        // 3. Filtrado por zona
        if (request.DeliveryZoneId.HasValue)
        {
            query = query.Where(o => o.DeliveryZoneId == request.DeliveryZoneId.Value);
        }

        // 4. Filtrado por usuario asignado
        if (!string.IsNullOrWhiteSpace(request.AssignedUserId))
        {
            var uid = request.AssignedUserId.Trim();
            query = query.Where(o =>
                o.FilledById == uid ||
                o.DeliveryManId == uid ||
                o.VerifiedById == uid ||
                o.TakenById == uid ||
                o.CapturedById == uid);
        }

        // 5. Filtrado por término de búsqueda
        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            var term = request.SearchTerm.Trim().ToLower();
            query = query.Where(o =>
                (o.Series != null && o.Series.ToLower().Contains(term)) ||
                o.Folio.ToString().Contains(term) ||
                (o.Client != null && o.Client.Name.ToLower().Contains(term)) ||
                (o.Client != null && o.Client.Phone != null && o.Client.Phone.Contains(term)) ||
                (o.GeneralNotes != null && o.GeneralNotes.ToLower().Contains(term)) ||
                (o.DeliveryNotes != null && o.DeliveryNotes.ToLower().Contains(term)));
        }

        var orders = await query
            .OrderByDescending(o => o.OrderDate)
            .ToListAsync(cancellationToken);

        // 6. Obtener catálogo de usuarios para mapeo de nombres
        var allUsers = await _identityService.GetUsersAsync(cancellationToken);
        var usersMap = allUsers.ToDictionary(u => u.Id, u => u, StringComparer.OrdinalIgnoreCase);

        var orderItems = new List<OrderMonitoringItemDto>(orders.Count);

        int pendingCount = 0;
        int inFulfillmentCount = 0;
        int filledCount = 0;
        int confirmedCount = 0;
        int routedCount = 0;
        int enRouteCount = 0;
        int deliveredTodayCount = 0;
        int returnedCount = 0;
        int cancelledCount = 0;
        int delayedCount = 0;

        double totalFulfillmentMinutes = 0;
        int fulfillmentCount = 0;
        double totalDeliveryMinutes = 0;
        int deliveryFinishedCount = 0;
        decimal totalActiveAmount = 0;

        foreach (var o in orders)
        {
            var statusEnteredAt = GetStatusEnteredAt(o);
            var isTerminal = o.Status == OrderStatus.Delivered || o.Status == OrderStatus.Settled || o.Status == OrderStatus.Cancelled || o.Status == OrderStatus.Returned;
            
            // Tiempo en estado actual
            double minutesInStatus;
            if (!isTerminal)
            {
                minutesInStatus = Math.Max(0, (now - statusEnteredAt).TotalMinutes);
            }
            else
            {
                var endDt = o.SettledAt ?? o.DeliveredAt ?? o.LastModifiedAt ?? now;
                minutesInStatus = Math.Max(0, (endDt - statusEnteredAt).TotalMinutes);
            }

            // Tiempo total transcurrido
            double totalMinutes;
            if (!isTerminal)
            {
                totalMinutes = Math.Max(0, (now - o.OrderDate).TotalMinutes);
            }
            else
            {
                var terminalEndDt = o.SettledAt ?? o.DeliveredAt ?? o.LastModifiedAt ?? now;
                totalMinutes = Math.Max(0, (terminalEndDt - o.OrderDate).TotalMinutes);
            }

            // Evaluación de SLA
            var (slaStatus, slaMessage) = CalculateSla(o.Status, minutesInStatus, totalMinutes);

            if (slaStatus == OrderSlaStatus.Delayed && !isTerminal)
            {
                delayedCount++;
            }

            // Resolución de usuarios
            usersMap.TryGetValue(o.TakenById ?? string.Empty, out var takenUser);
            usersMap.TryGetValue(o.CapturedById ?? string.Empty, out var capturedUser);
            usersMap.TryGetValue(o.FilledById ?? string.Empty, out var filledUser);
            usersMap.TryGetValue(o.VerifiedById ?? string.Empty, out var verifiedUser);
            usersMap.TryGetValue(o.DeliveryManId ?? string.Empty, out var deliveryUser);
            usersMap.TryGetValue(o.SettledById ?? string.Empty, out var settledUser);
            usersMap.TryGetValue(o.AuthorizedBySupervisorId ?? string.Empty, out var supervisorUser);

            var (assigneeName, assigneeRole) = GetCurrentAssignee(o, filledUser, deliveryUser, verifiedUser, takenUser);

            // Resumen de productos
            var summaryItems = o.Items.Take(3).Select(i => $"{i.Quantity:G29}x {i.ProductName}").ToList();
            if (o.Items.Count > 3)
            {
                summaryItems.Add($"(+{o.Items.Count - 3} más)");
            }
            var itemsSummary = string.Join(", ", summaryItems);

            // Contabilización de métricas
            switch (o.Status)
            {
                case OrderStatus.Pending:
                    pendingCount++;
                    totalActiveAmount += o.TotalAmount;
                    break;
                case OrderStatus.InFulfillment:
                    inFulfillmentCount++;
                    totalActiveAmount += o.TotalAmount;
                    break;
                case OrderStatus.Filled:
                    filledCount++;
                    totalActiveAmount += o.TotalAmount;
                    break;
                case OrderStatus.Confirmed:
                    confirmedCount++;
                    totalActiveAmount += o.TotalAmount;
                    break;
                case OrderStatus.Routed:
                    routedCount++;
                    totalActiveAmount += o.TotalAmount;
                    break;
                case OrderStatus.EnRoute:
                    enRouteCount++;
                    totalActiveAmount += o.TotalAmount;
                    break;
                case OrderStatus.Delivered:
                case OrderStatus.Settled:
                    if (o.OrderDate >= todayStart || (o.DeliveredAt.HasValue && o.DeliveredAt.Value >= todayStart))
                        deliveredTodayCount++;
                    break;
                case OrderStatus.Returned:
                    returnedCount++;
                    break;
                case OrderStatus.Cancelled:
                    cancelledCount++;
                    break;
            }

            // Tiempos promedio
            if (o.FulfillmentStartedAt.HasValue && o.FilledAt.HasValue)
            {
                var fMins = (o.FilledAt.Value - o.FulfillmentStartedAt.Value).TotalMinutes;
                if (fMins > 0 && fMins < 300)
                {
                    totalFulfillmentMinutes += fMins;
                    fulfillmentCount++;
                }
            }

            if (o.DispatchedAt.HasValue && o.DeliveredAt.HasValue)
            {
                var dMins = (o.DeliveredAt.Value - o.DispatchedAt.Value).TotalMinutes;
                if (dMins > 0 && dMins < 300)
                {
                    totalDeliveryMinutes += dMins;
                    deliveryFinishedCount++;
                }
            }

            var itemDto = new OrderMonitoringItemDto
            {
                Id = o.Id,
                OrderNumber = $"{o.Series ?? "PED"}-{o.Folio}",
                Series = o.Series,
                Folio = o.Folio,
                OrderDate = o.OrderDate,
                Status = o.Status,
                Channel = o.Channel,
                PaymentMethod = o.PaymentMethod,
                PaymentMethodDisplay = GetPaymentMethodDisplay(o.PaymentMethod),
                TotalAmount = o.TotalAmount,
                Subtotal = o.Subtotal,
                TotalTax = o.TotalTax,
                ItemCount = o.Items.Count,
                ItemsSummary = itemsSummary,
                ClientId = o.ClientId,
                ClientName = o.Client?.Name ?? "Público General",
                ClientPhone = o.Client?.Phone,
                ClientAddress = o.Client?.Address?.ToFullAddressString(),
                DeliveryZoneId = o.DeliveryZoneId,
                DeliveryZoneName = o.DeliveryZone?.Name,
                IsOutOfZone = o.IsOutOfZone,
                GeneralNotes = o.GeneralNotes,
                DeliveryNotes = o.DeliveryNotes,
                ReturnReason = o.ReturnReason,
                CancellationReason = o.CancellationReason,
                DeliveryRouteId = o.DeliveryRouteId,
                DeliveryRouteFolio = o.DeliveryRoute?.Folio,
                DeliveryRouteStatus = o.DeliveryRoute?.Status,

                TakenById = o.TakenById,
                TakenByName = takenUser?.FullName ?? (o.CreatedBy ?? "—"),
                CapturedById = o.CapturedById,
                CapturedByName = capturedUser?.FullName,
                FilledById = o.FilledById,
                FilledByName = filledUser?.FullName,
                FilledByEmployeeNumber = filledUser?.EmployeeNumber,
                VerifiedById = o.VerifiedById,
                VerifiedByName = verifiedUser?.FullName,
                DeliveryManId = o.DeliveryManId,
                DeliveryManName = deliveryUser?.FullName,
                DeliveryManEmployeeNumber = deliveryUser?.EmployeeNumber,
                SettledById = o.SettledById,
                SettledByName = settledUser?.FullName,
                AuthorizedBySupervisorId = o.AuthorizedBySupervisorId,
                AuthorizedBySupervisorName = supervisorUser?.FullName,

                CurrentAssigneeName = assigneeName,
                CurrentAssigneeRole = assigneeRole,

                FulfillmentStartedAt = o.FulfillmentStartedAt,
                FilledAt = o.FilledAt,
                VerifiedAt = o.VerifiedAt,
                DispatchedAt = o.DispatchedAt,
                DeliveredAt = o.DeliveredAt,
                SettledAt = o.SettledAt,
                StatusEnteredAt = statusEnteredAt,

                MinutesInCurrentStatus = minutesInStatus,
                FormattedTimeInCurrentStatus = FormatDuration(minutesInStatus),
                TotalMinutesElapsed = totalMinutes,
                FormattedTotalElapsed = FormatDuration(totalMinutes),
                SlaStatus = slaStatus,
                SlaMessage = slaMessage,

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
            };

            // Filtrar si solo se solicitaron pedidos retrasados
            if (request.OnlyDelayed && slaStatus == OrderSlaStatus.Normal)
            {
                continue;
            }

            orderItems.Add(itemDto);
        }

        var activeOrdersCount = pendingCount + inFulfillmentCount + filledCount + confirmedCount + routedCount + enRouteCount;

        var summary = new OrderMonitoringSummaryDto
        {
            TotalOrders = orders.Count,
            ActiveOrders = activeOrdersCount,
            PendingOrders = pendingCount,
            InFulfillmentOrders = inFulfillmentCount,
            FilledOrders = filledCount,
            ConfirmedOrders = confirmedCount,
            RoutedOrders = routedCount,
            EnRouteOrders = enRouteCount,
            DeliveredTodayOrders = deliveredTodayCount,
            ReturnedOrders = returnedCount,
            CancelledOrders = cancelledCount,
            DelayedOrdersCount = delayedCount,
            AverageFulfillmentMinutes = fulfillmentCount > 0 ? Math.Round(totalFulfillmentMinutes / fulfillmentCount, 1) : 0,
            AverageDeliveryMinutes = deliveryFinishedCount > 0 ? Math.Round(totalDeliveryMinutes / deliveryFinishedCount, 1) : 0,
            TotalActiveAmount = totalActiveAmount
        };

        return new OrderMonitoringResultDto
        {
            Summary = summary,
            Orders = orderItems
        };
    }

    private static DateTime GetStatusEnteredAt(Order order)
    {
        return order.Status switch
        {
            OrderStatus.Pending => order.OrderDate,
            OrderStatus.InFulfillment => order.FulfillmentStartedAt ?? order.OrderDate,
            OrderStatus.Filled => order.FilledAt ?? order.FulfillmentStartedAt ?? order.OrderDate,
            OrderStatus.Confirmed => order.VerifiedAt ?? order.FilledAt ?? order.OrderDate,
            OrderStatus.Routed => order.VerifiedAt ?? order.FilledAt ?? order.OrderDate,
            OrderStatus.EnRoute => order.DispatchedAt ?? order.VerifiedAt ?? order.OrderDate,
            OrderStatus.Delivered => order.DeliveredAt ?? order.DispatchedAt ?? order.OrderDate,
            OrderStatus.Returned => order.DeliveredAt ?? order.DispatchedAt ?? order.OrderDate,
            OrderStatus.Settled => order.SettledAt ?? order.DeliveredAt ?? order.OrderDate,
            OrderStatus.Cancelled => order.LastModifiedAt ?? order.OrderDate,
            _ => order.OrderDate
        };
    }

    private static (OrderSlaStatus Status, string Message) CalculateSla(OrderStatus status, double minutesInStatus, double totalMinutes)
    {
        return status switch
        {
            OrderStatus.Pending => minutesInStatus switch
            {
                > 20 => (OrderSlaStatus.Delayed, "Demora crítica sin surtidor (>20 min)"),
                > 10 => (OrderSlaStatus.Warning, "Pendiente de asignar (>10 min)"),
                _ => (OrderSlaStatus.Normal, "En tiempo")
            },
            OrderStatus.InFulfillment => minutesInStatus switch
            {
                > 30 => (OrderSlaStatus.Delayed, "Surtido crítico demorado (>30 min)"),
                > 15 => (OrderSlaStatus.Warning, "Surtido prolongado (>15 min)"),
                _ => (OrderSlaStatus.Normal, "En tiempo")
            },
            OrderStatus.Filled => minutesInStatus switch
            {
                > 20 => (OrderSlaStatus.Delayed, "Retraso en verificación de caja (>20 min)"),
                > 10 => (OrderSlaStatus.Warning, "Listo por verificar (>10 min)"),
                _ => (OrderSlaStatus.Normal, "En tiempo")
            },
            OrderStatus.Confirmed or OrderStatus.Routed => minutesInStatus switch
            {
                > 30 => (OrderSlaStatus.Delayed, "Demora en despacho / ruteo (>30 min)"),
                > 15 => (OrderSlaStatus.Warning, "Listo para despacho (>15 min)"),
                _ => (OrderSlaStatus.Normal, "En tiempo")
            },
            OrderStatus.EnRoute => minutesInStatus switch
            {
                > 50 => (OrderSlaStatus.Delayed, "Entrega demorada en ruta (>50 min)"),
                > 35 => (OrderSlaStatus.Warning, "En reparto prolongado (>35 min)"),
                _ => (OrderSlaStatus.Normal, "En camino")
            },
            OrderStatus.Delivered => (OrderSlaStatus.Normal, "Entregado con éxito"),
            OrderStatus.Returned => (OrderSlaStatus.Delayed, "Devuelto / No entregado"),
            OrderStatus.Settled => (OrderSlaStatus.Normal, "Liquidado"),
            OrderStatus.Cancelled => (OrderSlaStatus.Delayed, "Cancelado"),
            _ => (OrderSlaStatus.Normal, "Normal")
        };
    }

    private static (string Name, string Role) GetCurrentAssignee(
        Order order,
        UserSyncDataDto? filledUser,
        UserSyncDataDto? deliveryUser,
        UserSyncDataDto? verifiedUser,
        UserSyncDataDto? takenUser)
    {
        return order.Status switch
        {
            OrderStatus.Pending => ("Sin Surtidor Asignado", "En Espera"),
            OrderStatus.InFulfillment => (filledUser?.FullName ?? order.FilledById ?? "Surtidor Asignado", "Surtidor"),
            OrderStatus.Filled => (filledUser != null ? $"Surtido por {filledUser.FullName}" : "Listo en Mostrador", "Por Verificar"),
            OrderStatus.Confirmed => (verifiedUser != null ? $"Verificado por {verifiedUser.FullName}" : "Por Asignar Ruta", "Logística"),
            OrderStatus.Routed => (order.DeliveryRoute != null ? $"Ruta #{order.DeliveryRoute.Folio}" : "Enrutado", "Por Despachar"),
            OrderStatus.EnRoute => (deliveryUser?.FullName ?? order.DeliveryManId ?? "Repartidor en Camino", "Repartidor"),
            OrderStatus.Delivered => (deliveryUser?.FullName ?? "Entregado al Cliente", "Entregado"),
            OrderStatus.Returned => (order.ReturnReason ?? "No entregado / Devuelto", "Incidencia"),
            OrderStatus.Settled => ("Liquidado en Sucursal", "Cerrado"),
            OrderStatus.Cancelled => (order.CancellationReason ?? "Cancelado", "Cancelado"),
            _ => ("Sin Asignar", "—")
        };
    }

    private static string FormatDuration(double totalMinutes)
    {
        if (totalMinutes < 1) return "< 1 min";
        if (totalMinutes < 60) return $"{(int)totalMinutes} min";
        
        int hours = (int)(totalMinutes / 60);
        int mins = (int)(totalMinutes % 60);
        
        return mins > 0 ? $"{hours}h {mins}m" : $"{hours}h";
    }

    private static string GetPaymentMethodDisplay(PaymentMethodType method) => method switch
    {
        PaymentMethodType.Cash => "Efectivo",
        PaymentMethodType.CreditCard => "Tarjeta de Crédito",
        PaymentMethodType.DebitCard => "Tarjeta de Débito",
        PaymentMethodType.Transfer => "Transferencia SPEI",
        PaymentMethodType.Check => "Cheque",
        _ => method.ToString()
    };
}
