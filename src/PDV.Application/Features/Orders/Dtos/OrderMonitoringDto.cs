using System;
using System.Collections.Generic;
using PDV.Domain.Enums;

namespace PDV.Application.Features.Orders.Dtos;

public enum OrderSlaStatus
{
    Normal = 0,   // Dentro del tiempo esperado (Verde)
    Warning = 1,  // Próximo a vencer / Atención (Amarillo)
    Delayed = 2   // Fuera de tiempo / Retrasado (Rojo)
}

public class OrderMonitoringItemDto
{
    public Guid Id { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public string? Series { get; set; }
    public int Folio { get; set; }
    public DateTime OrderDate { get; set; }
    public OrderStatus Status { get; set; }
    public OrderChannel Channel { get; set; } = OrderChannel.Telephone;
    public PaymentMethodType PaymentMethod { get; set; }
    public string PaymentMethodDisplay { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public decimal Subtotal { get; set; }
    public decimal TotalTax { get; set; }
    public int ItemCount { get; set; }
    public string ItemsSummary { get; set; } = string.Empty;
    public List<OrderItemDto> Items { get; set; } = new();

    // Datos del Cliente
    public Guid? ClientId { get; set; }
    public string ClientName { get; set; } = string.Empty;
    public string? ClientPhone { get; set; }
    public string? ClientAddress { get; set; }
    public Guid? DeliveryZoneId { get; set; }
    public string? DeliveryZoneName { get; set; }
    public bool IsOutOfZone { get; set; }

    // Notas e incidencias
    public string? GeneralNotes { get; set; }
    public string? DeliveryNotes { get; set; }
    public string? ReturnReason { get; set; }
    public string? CancellationReason { get; set; }

    // Ruta de reparto
    public Guid? DeliveryRouteId { get; set; }
    public int? DeliveryRouteFolio { get; set; }
    public DeliveryRouteStatus? DeliveryRouteStatus { get; set; }

    // Personal asignado e involucrado
    public string? TakenById { get; set; }
    public string? TakenByName { get; set; }
    public string? CapturedById { get; set; }
    public string? CapturedByName { get; set; }
    public string? FilledById { get; set; }
    public string? FilledByName { get; set; }
    public string? FilledByEmployeeNumber { get; set; }
    public string? VerifiedById { get; set; }
    public string? VerifiedByName { get; set; }
    public string? DeliveryManId { get; set; }
    public string? DeliveryManName { get; set; }
    public string? DeliveryManEmployeeNumber { get; set; }
    public string? SettledById { get; set; }
    public string? SettledByName { get; set; }
    public string? AuthorizedBySupervisorId { get; set; }
    public string? AuthorizedBySupervisorName { get; set; }

    public string CurrentAssigneeName { get; set; } = "Sin asignar";
    public string CurrentAssigneeRole { get; set; } = string.Empty;

    // Hitos y Tiempos de Auditoría
    public DateTime? FulfillmentStartedAt { get; set; }
    public DateTime? FilledAt { get; set; }
    public DateTime? VerifiedAt { get; set; }
    public DateTime? DispatchedAt { get; set; }
    public DateTime? DeliveredAt { get; set; }
    public DateTime? SettledAt { get; set; }
    public DateTime StatusEnteredAt { get; set; }

    // Tiempos calculados
    public double MinutesInCurrentStatus { get; set; }
    public string FormattedTimeInCurrentStatus { get; set; } = "0 min";
    public double TotalMinutesElapsed { get; set; }
    public string FormattedTotalElapsed { get; set; } = "0 min";
    public OrderSlaStatus SlaStatus { get; set; } = OrderSlaStatus.Normal;
    public string SlaMessage { get; set; } = string.Empty;
}

public class OrderMonitoringSummaryDto
{
    public int TotalOrders { get; set; }
    public int ActiveOrders { get; set; }
    public int PendingOrders { get; set; }
    public int InFulfillmentOrders { get; set; }
    public int FilledOrders { get; set; }
    public int ConfirmedOrders { get; set; }
    public int RoutedOrders { get; set; }
    public int EnRouteOrders { get; set; }
    public int DeliveredTodayOrders { get; set; }
    public int ReturnedOrders { get; set; }
    public int CancelledOrders { get; set; }
    public int DelayedOrdersCount { get; set; }
    public double AverageFulfillmentMinutes { get; set; }
    public double AverageDeliveryMinutes { get; set; }
    public decimal TotalActiveAmount { get; set; }
}

public class OrderMonitoringResultDto
{
    public OrderMonitoringSummaryDto Summary { get; set; } = new();
    public List<OrderMonitoringItemDto> Orders { get; set; } = new();
}
