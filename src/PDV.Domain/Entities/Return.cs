using PDV.Domain.Common;
using PDV.Domain.Enums;
using PDV.Domain.Events;
using PDV.Domain.Exceptions;
using PDV.Domain.ValueObjects;

namespace PDV.Domain.Entities;

/// <summary>
/// Agregado raíz que representa una Devolución. Espejo inverso de Sale.
/// Puede ser una devolución total (toda la venta) o parcial (uno o más ítems).
/// </summary>
public class Return : BaseEntity, IAggregateRoot
{
    private readonly List<ReturnItem> _items = new();
    private readonly List<TaxBreakdown> _taxes = new();

    // ──────────────────────────────────────────────
    // Identificación y Contexto
    // ──────────────────────────────────────────────
    public string? Series { get; private set; }
    public int Folio { get; private set; }
    public DateTime ReturnDate { get; private set; }
    public string Reason { get; private set; }
    public RefundMethod RefundMethod { get; private set; }

    /// <summary>Turno en el que se registra la devolución.</summary>
    public Guid ShiftId { get; private set; }
    public Shift? Shift { get; private set; }

    /// <summary>Venta original a la que aplica la devolución.</summary>
    public Guid? SaleId { get; private set; }
    public Sale? Sale { get; private set; }

    public Guid? CashRegisterId { get; private set; }
    public CashRegister? CashRegister { get; private set; }
    public Guid BranchId { get; private set; }
    public Branch? Branch { get; private set; }

    public Guid? ClientId { get; private set; }
    public Client? Client { get; private set; }

    // ──────────────────────────────────────────────
    // Usuario y Empleado que autoriza
    // ──────────────────────────────────────────────
    public string UserId { get; private set; }
    public Guid? EmployeeId { get; private set; }
    public Employee? Employee { get; private set; }

    // ──────────────────────────────────────────────
    // Totales calculados — espejo de Sale
    // ──────────────────────────────────────────────
    public decimal Subtotal { get; private set; }
    public decimal TotalTax { get; private set; }
    public decimal TotalRefund { get; private set; }

    public bool IsCompleted { get; private set; }

    // ──────────────────────────────────────────────
    // Colecciones
    // ──────────────────────────────────────────────
    public IReadOnlyCollection<ReturnItem> Items => _items.AsReadOnly();
    public IReadOnlyCollection<TaxBreakdown> Taxes => _taxes.AsReadOnly();

#pragma warning disable CS8618
    private Return() { } // Para EF Core
#pragma warning restore CS8618

    public Return(
        string reason,
        RefundMethod refundMethod,
        string userId,
        Guid shiftId,
        string? series = null,
        int folio = 0,
        Guid? saleId = null,
        Guid? clientId = null,
        Guid? cashRegisterId = null,
        Guid? employeeId = null)
    {
        if (string.IsNullOrWhiteSpace(reason)) throw new DomainException("Se requiere un motivo para la devolución.");
        if (string.IsNullOrWhiteSpace(userId)) throw new DomainException("El ID de usuario es requerido.");
        if (shiftId == Guid.Empty) throw new DomainException("El ID de turno es requerido para registrar una devolución.");

        Reason = reason.Trim();
        RefundMethod = refundMethod;
        UserId = userId;
        ShiftId = shiftId;
        Series = series;
        Folio = folio;
        SaleId = saleId;
        ClientId = clientId;
        CashRegisterId = cashRegisterId;
        EmployeeId = employeeId;
        ReturnDate = DateTime.UtcNow;
        IsCompleted = false;
        Subtotal = 0;
        TotalTax = 0;
        TotalRefund = 0;

        AddDomainEvent(new ReturnRegisteredEvent(Id, SaleId, null, 0, Reason));
    }

    // ──────────────────────────────────────────────
    // Gestión de Ítems — espejo de Sale.AddItem / RemoveItem
    // ──────────────────────────────────────────────

    public void AddItem(ReturnItem item)
    {
        if (item == null) throw new DomainException("El ítem de devolución no puede ser nulo.");
        if (IsCompleted) throw new DomainException("No se pueden agregar ítems a una devolución ya completada.");

        item.SetReturn(this);
        _items.Add(item);
        RecalculateTotals();

        AddDomainEvent(new ReturnItemAddedEvent(Id, item.ProductId, item.Quantity));
    }

    public void RemoveItem(Guid itemId)
    {
        if (IsCompleted) throw new DomainException("No se pueden quitar ítems de una devolución ya completada.");

        var item = _items.FirstOrDefault(i => i.Id == itemId);
        if (item == null) throw new DomainException($"El ítem con ID {itemId} no existe en esta devolución.");

        _items.Remove(item);
        RecalculateTotals();

        AddDomainEvent(new ReturnItemRemovedEvent(Id, item.ProductId));
    }
    public void SetBranch(Guid branchId)
    {
        if (branchId == Guid.Empty) throw new DomainException("El ID de sucursal debe ser mayor a cero.");
        BranchId = branchId;
    }

    // ──────────────────────────────────────────────
    // Completar — espejo de Sale.MarkAsPaid
    // ──────────────────────────────────────────────

    public void Complete()
    {
        if (IsCompleted) throw new DomainException("La devolución ya fue completada.");
        if (_items.Count == 0) throw new DomainException("No se puede completar una devolución sin ítems.");

        IsCompleted = true;
        AddDomainEvent(new ReturnCompletedEvent(Id, SaleId, TotalRefund, RefundMethod));
    }

    // ──────────────────────────────────────────────
    // Recálculo de totales — espejo de Sale.RecalculateTotals
    // ──────────────────────────────────────────────

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
        TotalRefund = Subtotal + TotalTax;
    }
}
