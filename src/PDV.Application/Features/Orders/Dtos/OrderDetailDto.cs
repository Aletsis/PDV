using System;
using System.Collections.Generic;
using PDV.Domain.Entities;
using PDV.Domain.Enums;

namespace PDV.Application.Features.Orders.Dtos;

public class OrderDetailDto
{
    public Guid Id { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public DateTime Date { get; set; }
    public decimal TotalAmount { get; set; }
    public string PaymentMethod { get; set; } = string.Empty;
    public Guid? ClientId { get; set; }
    public string ClientName { get; set; } = string.Empty;
    public string? ClientAddress { get; set; }
    public string? ClientPhone { get; set; }
    public bool IsCancelled { get; set; }
    public OrderStatus Status { get; set; }
    public OrderChannel Channel { get; set; } = OrderChannel.Telephone;
    public string? Series { get; set; }
    public int Folio { get; set; }
    public Guid? ShiftId { get; set; }
    public Guid? CashRegisterId { get; set; }
    public Guid? DeliveryZoneId { get; set; }
    public string? DeliveryZoneName { get; set; }
    public Guid? DeliveryRouteId { get; set; }
    public int? DeliveryRouteFolio { get; set; }
    public string? DeliveryManId { get; set; }
    public string? DeliveryManName { get; set; }
    public string? TakenById { get; set; }
    public string? FilledById { get; set; }
    public string? VerifiedById { get; set; }
    public string? SettledById { get; set; }
    public string? GeneralNotes { get; set; }
    public string? DeliveryNotes { get; set; }
    public string? ReturnReason { get; set; }
    public string? CancellationReason { get; set; }
    public bool IsOutOfZone { get; set; }
    public DateTime? FulfillmentStartedAt { get; set; }
    public DateTime? FilledAt { get; set; }
    public DateTime? VerifiedAt { get; set; }
    public DateTime? DispatchedAt { get; set; }
    public DateTime? DeliveredAt { get; set; }
    public DateTime? SettledAt { get; set; }
    public List<OrderItemDto> Items { get; set; } = new();
}