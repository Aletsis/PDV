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
    public bool IsCancelled { get; set; }
    public OrderStatus Status { get; set; }
    public string? Series { get; set; }
    public int Folio { get; set; }
    public Guid ShiftId { get; set; }
    public List<OrderItemDto> Items { get; set; } = new();
}