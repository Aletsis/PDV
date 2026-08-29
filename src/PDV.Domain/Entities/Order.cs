using PDV.Domain.Common;
using PDV.Domain.Events;
using PDV.Domain.Exceptions;
using PDV.Domain.Enums;
using PDV.Domain.ValueObjects;

namespace PDV.Domain.Entities;

public class Order : BaseEntity, IAggregateRoot
{
    private readonly List<OrderItem> _items = new();
    private readonly List<TaxBreakdown> _taxes = new();

    public Guid? ClientId { get; private set; }
    public Client? Client { get; private set; }
    
    public Guid? CashRegisterId { get; private set; }
    public CashRegister? CashRegister { get; private set; }

    public Guid BranchId { get; private set; }
    public Branch? Branch { get; private set; }

    public Guid? ShiftId { get; private set; }
    public Shift? Shift { get; private set; }

    public string? Series { get; private set; }
    public int Folio { get; private set; }

    // Asignaciones y personal
    public Guid? DeliveryRouteId { get; private set; }
    public DeliveryRoute? DeliveryRoute { get; private set; }
    public Guid? DeliveryZoneId { get; private set; }
    public DeliveryZone? DeliveryZone { get; private set; }
    public bool IsOutOfZone { get; private set; }
    public string? DeliveryManId { get; private set; }
    public string? TakenById { get; private set; }
    public string? FilledById { get; private set; }
    public string? CapturedById { get; private set; }
    public string? VerifiedById { get; private set; }
    public string? RoutedById { get; private set; }
    public string? SettledById { get; private set; }

    // Notas e incidencias
    public string? GeneralNotes { get; private set; }
    public string? DeliveryNotes { get; private set; }
    public string? ReturnReason { get; private set; }
    public string? CancellationReason { get; private set; }

    // Hitos y Tiempos de Auditoría
    public DateTime OrderDate { get; private set; }
    public DateTime? FulfillmentStartedAt { get; private set; }
    public DateTime? FilledAt { get; private set; }
    public DateTime? VerifiedAt { get; private set; }
    public DateTime? DispatchedAt { get; private set; }
    public DateTime? DeliveredAt { get; private set; }
    public DateTime? SettledAt { get; private set; }

    public OrderStatus Status { get; private set; } = OrderStatus.Pending;
    public PaymentMethodType PaymentMethod { get; private set; }
    
    public decimal Subtotal { get; private set; }
    public decimal TotalTax { get; private set; }
    public decimal TotalAmount { get; private set; }
    
    public bool IsInvoiceRequested { get; private set; }
    public string? AuthorizedBySupervisorId { get; private set; }

    public bool IsCancelled => Status == OrderStatus.Cancelled;
    public bool IsEditable => Status == OrderStatus.Pending || Status == OrderStatus.InFulfillment || Status == OrderStatus.Filled || Status == OrderStatus.Confirmed;

    public IReadOnlyCollection<OrderItem> Items => _items.AsReadOnly();
    public IReadOnlyCollection<TaxBreakdown> Taxes => _taxes.AsReadOnly();

#pragma warning disable CS8618
    private Order() { } // For EF Core
#pragma warning restore CS8618

    public Order(
        Guid branchId,
        Guid? cashRegisterId,
        Guid? shiftId,
        Guid? clientId,
        PaymentMethodType paymentMethod,
        Guid? deliveryZoneId = null,
        string? takenById = null,
        string? capturedById = null,
        string? series = null,
        int folio = 0,
        string? generalNotes = null,
        string? deliveryNotes = null,
        bool isOutOfZone = false)
    {
        if (branchId == Guid.Empty) throw new DomainException("El ID de sucursal es requerido.");

        BranchId = branchId;
        CashRegisterId = cashRegisterId;
        ShiftId = shiftId;

        ClientId = clientId;
        PaymentMethod = paymentMethod;
        DeliveryZoneId = deliveryZoneId;
        TakenById = takenById;
        CapturedById = capturedById;
        Series = series;
        Folio = folio;
        GeneralNotes = generalNotes?.Trim();
        DeliveryNotes = deliveryNotes?.Trim();
        IsOutOfZone = isOutOfZone;
        
        OrderDate = DateTime.UtcNow;
        Status = OrderStatus.Pending;
        Subtotal = 0;
        TotalTax = 0;
        TotalAmount = 0;
        IsInvoiceRequested = false;
        AuthorizedBySupervisorId = null;
        
        AddDomainEvent(new OrderCreatedEvent(Id, ClientId));
    }

    public void RequestInvoice()
    {
        if (IsInvoiceRequested) throw new DomainException("El pedido ya tiene una factura solicitada.");
        if (ClientId == null) throw new DomainException("No se puede solicitar factura sin un cliente asociado.");
        if (Status == OrderStatus.Cancelled || Status == OrderStatus.Returned) 
            throw new DomainException("No se puede solicitar factura de un pedido cancelado o devuelto.");

        IsInvoiceRequested = true;
        AddDomainEvent(new OrderInvoiceRequestedEvent(Id));
    }

    public void AddItem(OrderItem item)
    {
        if (item == null) throw new DomainException("El item del pedido no puede ser nulo.");
        if (!IsEditable) throw new DomainException("No se pueden agregar artículos a un pedido en su estado actual.");

        _items.Add(item);
        RecalculateTotals();
        
        AddDomainEvent(new OrderItemAddedEvent(Id, item.ProductId, item.Quantity));
    }
    
    public void RemoveItem(Guid productId)
    {
        if (!IsEditable) throw new DomainException("No se pueden remover artículos de un pedido en su estado actual.");
        
        var item = _items.FirstOrDefault(i => i.ProductId == productId);
        if (item == null) throw new DomainException($"El producto con ID {productId} no existe en el pedido.");
        
        _items.Remove(item);
        RecalculateTotals();
        
        AddDomainEvent(new OrderItemRemovedEvent(Id, productId));
    }

    public void AuthorizeUnderMinimum(string supervisorId)
    {
        if (string.IsNullOrWhiteSpace(supervisorId)) throw new DomainException("Se requiere el ID del supervisor para autorizar.");
        if (!IsEditable) throw new DomainException("Solo se pueden autorizar pedidos editables.");

        AuthorizedBySupervisorId = supervisorId;
        AddDomainEvent(new OrderAuthorizedEvent(Id, supervisorId));
    }

    public void AssignPicker(string pickerId)
    {
        if (string.IsNullOrWhiteSpace(pickerId)) throw new DomainException("El ID del surtidor es requerido.");
        if (Status != OrderStatus.Pending && Status != OrderStatus.InFulfillment)
            throw new DomainException("Solo los pedidos pendientes pueden ser asignados a un surtidor.");

        FilledById = pickerId;
        FulfillmentStartedAt = DateTime.UtcNow;
        Status = OrderStatus.InFulfillment;
        AddDomainEvent(new OrderFulfillmentStartedEvent(Id, pickerId));
    }

    public void MarkAsFilled(string? pickerId = null)
    {
        if (Status != OrderStatus.Pending && Status != OrderStatus.InFulfillment)
            throw new DomainException("El pedido debe estar pendiente o en surtido para marcarse como surtido.");

        if (!string.IsNullOrWhiteSpace(pickerId))
        {
            FilledById = pickerId;
        }

        FilledAt = DateTime.UtcNow;
        Status = OrderStatus.Filled;
        AddDomainEvent(new OrderFilledEvent(Id, FilledById));
    }

    public void VerifyOrder(string verifierId, Guid? cashRegisterId = null, Guid? shiftId = null, decimal minimumRequiredAmount = 0)
    {
        if (string.IsNullOrWhiteSpace(verifierId)) throw new DomainException("El ID del verificador es requerido.");
        if (_items.Count == 0) throw new DomainException("No se puede verificar un pedido sin artículos.");

        if (TotalAmount < minimumRequiredAmount && string.IsNullOrWhiteSpace(AuthorizedBySupervisorId))
        {
            throw new DomainException($"El pedido no alcanza el monto mínimo de {minimumRequiredAmount:C}. Requiere autorización de un supervisor.");
        }

        VerifiedById = verifierId;
        CapturedById ??= verifierId;
        VerifiedAt = DateTime.UtcNow;

        if (cashRegisterId.HasValue) CashRegisterId = cashRegisterId;
        if (shiftId.HasValue) ShiftId = shiftId;

        Status = OrderStatus.Confirmed;
        RecalculateTotals();

        AddDomainEvent(new OrderVerifiedEvent(Id, verifierId));
        AddDomainEvent(new OrderConfirmedEvent(Id));
    }

    public void Confirm(decimal minimumRequiredAmount = 0)
    {
        if (Status != OrderStatus.Pending && Status != OrderStatus.InFulfillment && Status != OrderStatus.Filled && Status != OrderStatus.Confirmed)
            throw new DomainException("El pedido no se encuentra en un estado confirmable.");

        if (_items.Count == 0) throw new DomainException("No se puede confirmar un pedido sin artículos.");

        if (TotalAmount < minimumRequiredAmount && string.IsNullOrWhiteSpace(AuthorizedBySupervisorId))
        {
            throw new DomainException($"El pedido no alcanza el monto mínimo de {minimumRequiredAmount:C}. Requiere autorización de un supervisor.");
        }

        Status = OrderStatus.Confirmed;
        AddDomainEvent(new OrderConfirmedEvent(Id));
    }

    public void AssignRoute(Guid routeId, string routedById)
    {
        if (Status != OrderStatus.Confirmed) throw new DomainException("El pedido debe estar verificado/confirmado para ser enrutado.");

        DeliveryRouteId = routeId;
        RoutedById = routedById;
        Status = OrderStatus.Routed;
        AddDomainEvent(new OrderRoutedEvent(Id, routeId.ToString(), routedById));
    }

    public void AssignDeliveryMan(string deliveryManId)
    {
        if (!DeliveryRouteId.HasValue) throw new DomainException("Debe estar enrutado primero.");
        DeliveryManId = deliveryManId;
        DispatchedAt = DateTime.UtcNow;
        Status = OrderStatus.EnRoute;
        AddDomainEvent(new OrderDeliveryAssignedEvent(Id, deliveryManId));
    }

    public void MarkAsDelivered()
    {
        if (Status != OrderStatus.EnRoute) throw new DomainException("Solo un pedido en ruta puede ser entregado.");
        DeliveredAt = DateTime.UtcNow;
        Status = OrderStatus.Delivered;
        AddDomainEvent(new OrderDeliveredEvent(Id));
    }

    public void MarkAsReturned(string reason)
    {
        if (Status != OrderStatus.EnRoute) throw new DomainException("Solo un pedido en ruta puede ser devuelto.");
        if (string.IsNullOrWhiteSpace(reason)) throw new DomainException("Se requiere un motivo para registrar la devolución.");
        DeliveredAt = DateTime.UtcNow;
        Status = OrderStatus.Returned;
        ReturnReason = reason;
        AddDomainEvent(new OrderReturnedEvent(Id, reason));
    }

    public void Settle(string settledById)
    {
        if (Status != OrderStatus.Delivered && Status != OrderStatus.Returned)
            throw new DomainException("Solo pedidos entregados o devueltos pueden liquidarse.");

        SettledById = settledById;
        SettledAt = DateTime.UtcNow;
        Status = OrderStatus.Settled;
        AddDomainEvent(new OrderSettledEvent(Id, settledById));
    }
    
    public void Cancel(string reason)
    {
        if (string.IsNullOrWhiteSpace(reason)) throw new DomainException("Se requiere un motivo para cancelar el pedido.");
        if (Status == OrderStatus.Cancelled) throw new DomainException("El pedido ya está cancelado.");
        if (Status == OrderStatus.Delivered) throw new DomainException("Un pedido entregado no puede ser cancelado directamente.");
        
        CancellationReason = reason;
        Status = OrderStatus.Cancelled;
        AddDomainEvent(new OrderCancelledEvent(Id, reason));
    }

    public void SetGeneralNotes(string? notes)
    {
        GeneralNotes = notes?.Trim();
    }

    public void SetDeliveryNotes(string? notes)
    {
        DeliveryNotes = notes?.Trim();
    }

    public void SetOutOfZone(bool isOutOfZone)
    {
        IsOutOfZone = isOutOfZone;
    }

    public void SetDeliveryZone(Guid? zoneId)
    {
        DeliveryZoneId = zoneId;
    }

    public void SetCashRegisterAndShift(Guid cashRegisterId, Guid shiftId)
    {
        CashRegisterId = cashRegisterId;
        ShiftId = shiftId;
    }

    public void SetFolio(string series, int folio)
    {
        Series = series;
        Folio = folio;
    }

    private void RecalculateTotals()
    {
        Subtotal = _items.Sum(i => i.Quantity * i.UnitPrice);
        
        _taxes.Clear();
        
        var groupedTaxes = _items
            .GroupBy(i => new { i.TaxRate, i.IsTaxExempt })
            .Select(g => new TaxBreakdown(
                Rate: g.Key.TaxRate,
                BaseAmount: g.Sum(i => i.UnitPrice * i.Quantity),
                TaxAmount: g.Key.IsTaxExempt ? 0 : g.Sum(i => (i.UnitPrice * i.Quantity) * (g.Key.TaxRate / 100m)),
                IsExempt: g.Key.IsTaxExempt
            )).ToList();
            
        _taxes.AddRange(groupedTaxes);
        
        TotalTax = _taxes.Sum(t => t.TaxAmount);
        TotalAmount = Subtotal + TotalTax;
    }

    public void UpdateItemQuantity(Guid itemId, decimal newQuantity)
    {
        if (IsCancelled) throw new DomainException("No se puede modificar un pedido cancelado.");
        if (!IsEditable) throw new DomainException("No se puede modificar un pedido cerrado.");

        var item = _items.FirstOrDefault(i => i.Id == itemId);
        if (item == null) throw new DomainException($"El artículo con ID {itemId} no existe en el pedido.");

        item.UpdateQuantity(newQuantity);
        RecalculateTotals();
    }

    public void UpdateItemVerifiedQuantity(Guid itemId, decimal realWeightOrQuantity)
    {
        if (IsCancelled) throw new DomainException("No se puede modificar un pedido cancelado.");
        if (!IsEditable) throw new DomainException("No se puede modificar un pedido cerrado.");

        var item = _items.FirstOrDefault(i => i.Id == itemId);
        if (item == null) throw new DomainException($"El artículo con ID {itemId} no existe en el pedido.");

        item.SetVerifiedQuantity(realWeightOrQuantity);
        RecalculateTotals();
    }

    public void UpdateItemPrice(Guid itemId, decimal newPrice)
    {
        if (IsCancelled) throw new DomainException("No se puede modificar un pedido cancelado.");
        if (!IsEditable) throw new DomainException("No se puede modificar un pedido cerrado.");

        var item = _items.FirstOrDefault(i => i.Id == itemId);
        if (item == null) throw new DomainException($"El artículo con ID {itemId} no existe en el pedido.");

        item.OverridePrice(newPrice);
        RecalculateTotals();
    }

    public void SetBranch(Guid branchId)
    {
        if (branchId == Guid.Empty) throw new DomainException("El ID de sucursal debe ser mayor a cero.");
        BranchId = branchId;
    }
}
