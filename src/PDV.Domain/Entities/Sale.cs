using PDV.Domain.Common;
using PDV.Domain.Enums;
using PDV.Domain.Events;
using PDV.Domain.Exceptions;
using PDV.Domain.ValueObjects;

namespace PDV.Domain.Entities;

public class Sale : BaseEntity, IAggregateRoot
{
    private readonly List<SaleItem> _items = new();
    private readonly List<TaxBreakdown> _taxes = new();

    private SaleNumber _saleNumber = null!;

    public string SaleNumber 
    { 
        get => _saleNumber.Value; 
        private set => _saleNumber = (SaleNumber)value;
    }

    public string? Series { get; private set; }
    public int Folio { get; private set; }
    public Guid ShiftId { get; private set; }

    public DateTime Date { get; private set; } = DateTime.UtcNow;
    
    public PaymentMethodType PaymentMethod { get; private set; }
    
    public decimal Subtotal { get; private set; }
    public decimal TotalTax { get; private set; }
    public decimal TotalAmount { get; private set; }

    public string? UserId { get; private set; }
    public Guid? ClientId { get; private set; }
    public Client? Client { get; private set; }
    public Guid? CashRegisterId { get; private set; }
    public CashRegister? CashRegister { get; private set; }
    public Guid BranchId { get; private set; }
    public Branch? Branch { get; private set; }
    
    public bool IsPaid { get; private set; }
    public bool IsCancelled { get; private set; }
    public bool IsReturned { get; private set; }
    public bool IsInvoiceRequested { get; private set; }
    public bool IsInvoiced { get; private set; }
    public string? InvoiceId { get; private set; }
    
    public IReadOnlyCollection<SaleItem> Items => _items.AsReadOnly();
    public IReadOnlyCollection<TaxBreakdown> Taxes => _taxes.AsReadOnly();

#pragma warning disable CS8618
    private Sale() { } // For EF Core
#pragma warning restore CS8618

    public Sale(
        SaleNumber saleNumber,
        PaymentMethodType paymentMethod,
        string userId,
        Guid shiftId,
        string? series = null,
        int folio = 0,
        Guid? clientId = null,
        Guid? cashRegisterId = null)
    {
        if (string.IsNullOrWhiteSpace(userId))
            throw new DomainException("El ID de usuario es requerido.");
        if (shiftId == Guid.Empty)
            throw new DomainException("El ID de turno es requerido.");

        _saleNumber = saleNumber;
        PaymentMethod = paymentMethod;
        UserId = userId;
        ShiftId = shiftId;
        Series = series;
        Folio = folio;
        ClientId = clientId;
        CashRegisterId = cashRegisterId;
        Date = DateTime.UtcNow;
        IsPaid = false;
        IsCancelled = false;
        IsInvoiceRequested = false;
        IsInvoiced = false;
        InvoiceId = null;
        Subtotal = 0;
        TotalTax = 0;
        TotalAmount = 0;
        
        AddDomainEvent(new SaleCreatedEvent(Id, ClientId));
    }

    public void AddItem(SaleItem item)
    {
        if (item == null) throw new DomainException("El artículo no puede ser nulo.");
        if (IsCancelled) throw new DomainException("No se pueden agregar artículos a una venta cancelada.");
        if (IsPaid) throw new DomainException("No se pueden agregar artículos a una venta pagada.");

        item.SetSale(this);
        _items.Add(item);
        RecalculateTotals();
        
        AddDomainEvent(new SaleItemAddedEvent(Id, item.ProductId, item.Quantity));
    }

    public void RemoveItem(Guid itemId)
    {
        if (IsCancelled) throw new DomainException("No se pueden remover artículos de una venta cancelada.");
        if (IsPaid) throw new DomainException("No se pueden remover artículos de una venta pagada.");

        var item = _items.FirstOrDefault(i => i.Id == itemId);
        if (item == null) throw new DomainException($"El artículo con ID {itemId} no existe en la venta.");

        _items.Remove(item);
        RecalculateTotals();
        
        AddDomainEvent(new SaleItemRemovedEvent(Id, item.ProductId));
    }

    public void MarkAsPaid()
    {
        if (IsCancelled) throw new DomainException("No se puede marcar como pagada una venta cancelada.");
        if (IsPaid) throw new DomainException("La venta ya está marcada como pagada.");
        if (_items.Count == 0) throw new DomainException("No se puede marcar como pagada una venta sin artículos.");

        IsPaid = true;
        AddDomainEvent(new PaymentMadeEvent(Id, TotalAmount, PaymentMethod));
    }

    public void RequestInvoice()
    {
        if (IsInvoiced) throw new DomainException("La venta ya se encuentra facturada.");
        if (IsInvoiceRequested) throw new DomainException("La venta ya tiene una factura solicitada.");
        if (ClientId == null) throw new DomainException("No se puede solicitar factura sin un cliente asociado.");
        if (IsCancelled) throw new DomainException("No se puede solicitar factura de una venta cancelada.");

        IsInvoiceRequested = true;
        AddDomainEvent(new SaleInvoiceRequestedEvent(Id));
    }

    public void MarkAsInvoiced(string invoiceId)
    {
        if (string.IsNullOrWhiteSpace(invoiceId)) throw new DomainException("El ID de la factura es requerido.");
        if (IsInvoiced) throw new DomainException("La venta ya se encuentra facturada.");
        if (IsCancelled) throw new DomainException("No se puede facturar una venta cancelada.");

        IsInvoiced = true;
        InvoiceId = invoiceId;
        AddDomainEvent(new SaleInvoicedEvent(Id, invoiceId));
    }

    public void Cancel(string reason)
    {
        if (string.IsNullOrWhiteSpace(reason)) throw new DomainException("Se requiere un motivo para cancelar la venta.");
        if (IsCancelled) throw new DomainException("La venta ya está cancelada.");

        IsCancelled = true;
        AddDomainEvent(new SaleCancelledEvent(Id, reason));
    }

    public void MarkAsReturned()
    {
        IsReturned = true;
    }

    public void SetBranch(Guid branchId)
    {
        if (branchId == Guid.Empty) throw new DomainException("El ID de sucursal debe ser mayor a cero.");
        BranchId = branchId;
    }

    public void SetClient(Guid? clientId)
    {
        ClientId = clientId;
    }

    public void SetPaymentMethod(PaymentMethodType paymentMethod)
    {
        PaymentMethod = paymentMethod;
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

    public void LoadItems(IEnumerable<SaleItem> items)
    {
        _items.Clear();
        _items.AddRange(items);
        RecalculateTotals();
    }

    public void UpdateItemQuantity(Guid itemId, decimal newQuantity)
    {
        if (IsCancelled) throw new DomainException("No se puede modificar una venta cancelada.");
        if (IsPaid) throw new DomainException("No se puede modificar una venta pagada.");

        var item = _items.FirstOrDefault(i => i.Id == itemId);
        if (item == null) throw new DomainException($"El artículo con ID {itemId} no existe en la venta.");

        item.UpdateQuantity(newQuantity);
        RecalculateTotals();
    }

    public void UpdateItemPrice(Guid itemId, decimal newPrice)
    {
        if (IsCancelled) throw new DomainException("No se puede modificar una venta cancelada.");
        if (IsPaid) throw new DomainException("No se puede modificar una venta pagada.");

        var item = _items.FirstOrDefault(i => i.Id == itemId);
        if (item == null) throw new DomainException($"El artículo con ID {itemId} no existe en la venta.");

        item.OverridePrice(newPrice);
        RecalculateTotals();
    }
}
