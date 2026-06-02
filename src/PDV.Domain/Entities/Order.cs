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
    
    public Guid CashRegisterId { get; private set; }
    public CashRegister? CashRegister { get; private set; }

    public Guid BranchId { get; private set; }
    public Branch? Branch { get; private set; }

    public string? Series { get; private set; }
    public int Folio { get; private set; }

    // Asignaciones y personal
    public string? RouteId { get; private set; }
    public string? DeliveryManId { get; private set; }
    public string? TakenById { get; private set; }
    public string? FilledById { get; private set; }
    public string? CapturedById { get; private set; }
    public string? RoutedById { get; private set; }

    public DateTime OrderDate { get; private set; }
    public OrderStatus Status { get; private set; } = OrderStatus.Pending;
    public PaymentMethodType PaymentMethod { get; private set; }
    
    public decimal Subtotal { get; private set; }
    public decimal TotalTax { get; private set; }
    public decimal TotalAmount { get; private set; }
    
    public bool IsInvoiceRequested { get; private set; }
    public string? AuthorizedBySupervisorId { get; private set; }

    public IReadOnlyCollection<OrderItem> Items => _items.AsReadOnly();
    public IReadOnlyCollection<TaxBreakdown> Taxes => _taxes.AsReadOnly();

#pragma warning disable CS8618
    private Order() { } // For EF Core
#pragma warning restore CS8618

    public Order(
        Guid cashRegisterId,
        Guid branchId,
        Guid? clientId,
        PaymentMethodType paymentMethod,
        string? takenById = null,
        string? capturedById = null,
        string? series = null,
        int folio = 0)
    {
        if (cashRegisterId == Guid.Empty) throw new DomainException("El ID de caja es requerido.");
        if (branchId == Guid.Empty) throw new DomainException("El ID de sucursal es requerido.");

        CashRegisterId = cashRegisterId;
        BranchId = branchId;
        ClientId = clientId;
        PaymentMethod = paymentMethod;
        TakenById = takenById;
        CapturedById = capturedById;
        Series = series;
        Folio = folio;
        
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
        if (Status != OrderStatus.Pending) throw new DomainException("No se pueden agregar artículos a un pedido que no está pendiente.");

        _items.Add(item);
        RecalculateTotals();
        
        AddDomainEvent(new OrderItemAddedEvent(Id, item.ProductId, item.Quantity));
    }
    
    public void RemoveItem(Guid productId)
    {
        if (Status != OrderStatus.Pending) throw new DomainException("No se pueden remover artículos de un pedido que no está pendiente.");
        
        var item = _items.FirstOrDefault(i => i.ProductId == productId);
        if (item == null) throw new DomainException($"El producto con ID {productId} no existe en el pedido.");
        
        _items.Remove(item);
        RecalculateTotals();
        
        AddDomainEvent(new OrderItemRemovedEvent(Id, productId));
    }

    public void AuthorizeUnderMinimum(string supervisorId)
    {
        if (string.IsNullOrWhiteSpace(supervisorId)) throw new DomainException("Se requiere el ID del supervisor para autorizar.");
        if (Status != OrderStatus.Pending) throw new DomainException("Solo se pueden autorizar pedidos pendientes.");

        AuthorizedBySupervisorId = supervisorId;
        AddDomainEvent(new OrderAuthorizedEvent(Id, supervisorId));
    }

    public void Confirm(decimal minimumRequiredAmount = 0)
    {
        if (Status != OrderStatus.Pending) throw new DomainException("Solo los pedidos pendientes pueden ser confirmados.");
        if (_items.Count == 0) throw new DomainException("No se puede confirmar un pedido sin artículos.");

        if (TotalAmount < minimumRequiredAmount && string.IsNullOrWhiteSpace(AuthorizedBySupervisorId))
        {
            throw new DomainException($"El pedido no alcanza el monto mínimo de {minimumRequiredAmount:C}. Requiere autorización de un supervisor.");
        }

        Status = OrderStatus.Confirmed;
        AddDomainEvent(new OrderConfirmedEvent(Id));
    }

    public void AssignRoute(string routeId, string routedById)
    {
        if (Status != OrderStatus.Confirmed) throw new DomainException("El pedido debe estar confirmado para ser enrutado.");
        if (string.IsNullOrWhiteSpace(routeId)) throw new DomainException("El ID de la ruta es requerido.");

        RouteId = routeId;
        RoutedById = routedById;
        AddDomainEvent(new OrderRoutedEvent(Id, routeId, routedById));
    }

    public void AssignDeliveryMan(string deliveryManId)
    {
        if (string.IsNullOrWhiteSpace(RouteId)) throw new DomainException("Debe estar enrutado primero.");
        DeliveryManId = deliveryManId;
        Status = OrderStatus.EnRoute;
        AddDomainEvent(new OrderDeliveryAssignedEvent(Id, deliveryManId));
    }

    public void MarkAsDelivered()
    {
        if (Status != OrderStatus.EnRoute) throw new DomainException("Solo un pedido en ruta puede ser entregado.");
        Status = OrderStatus.Delivered;
        AddDomainEvent(new OrderDeliveredEvent(Id));
    }

    public void MarkAsReturned()
    {
        if (Status != OrderStatus.EnRoute) throw new DomainException("Solo un pedido en ruta puede ser devuelto.");
        Status = OrderStatus.Returned;
        AddDomainEvent(new OrderReturnedEvent(Id));
    }
    
    public void Cancel(string reason)
    {
        if (string.IsNullOrWhiteSpace(reason)) throw new DomainException("Se requiere un motivo para cancelar el pedido.");
        if (Status == OrderStatus.Cancelled) throw new DomainException("El pedido ya está cancelado.");
        if (Status == OrderStatus.Delivered) throw new DomainException("Un pedido entregado no puede ser cancelado directamente.");
        
        Status = OrderStatus.Cancelled;
        AddDomainEvent(new OrderCancelledEvent(Id, reason));
    }

    private void RecalculateTotals()
    {
        Subtotal = _items.Sum(i => i.Quantity * i.UnitPrice);
        
        _taxes.Clear();
        
        // Agrupar items por tasa de impuesto y exención
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
}
