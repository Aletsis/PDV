using PDV.Domain.Common;
using PDV.Domain.Enums;
using PDV.Domain.Events;
using PDV.Domain.Exceptions;
using PDV.Domain.ValueObjects;

namespace PDV.Domain.Entities;

public class Shift : BaseEntity, IAggregateRoot
{
    private readonly List<PaymentMethodBreakdown> _paymentMethodTotals = new();
    private readonly List<TaxBreakdown> _salesTaxTotals = new();
    private readonly List<TaxBreakdown> _returnsTaxTotals = new();
    private readonly List<ShiftCreditNote> _creditNotes = new();

    public Guid CashRegisterId { get; private set; }
    public CashRegister? CashRegister { get; private set; }
    
    public string UserId { get; private set; }
    
    public DateTime StartTime { get; private set; }
    public DateTime? EndTime { get; private set; }
    
    public decimal InitialCash { get; private set; }
    public decimal SystemExpectedCash { get; private set; }
    
    public decimal TotalCashReturns { get; private set; }
    
    public ShiftStatus Status { get; private set; }
    
    public bool IsGlobalInvoiceRequested { get; private set; }
    public bool IsGlobalInvoiced { get; private set; }
    public string? GlobalInvoiceId { get; private set; }
    public bool IsConsolidated { get; private set; }

    public IReadOnlyCollection<PaymentMethodBreakdown> PaymentMethodTotals => _paymentMethodTotals.AsReadOnly();
    public IReadOnlyCollection<TaxBreakdown> SalesTaxTotals => _salesTaxTotals.AsReadOnly();
    public IReadOnlyCollection<TaxBreakdown> ReturnsTaxTotals => _returnsTaxTotals.AsReadOnly();
    public IReadOnlyCollection<ShiftCreditNote> CreditNotes => _creditNotes.AsReadOnly();

#pragma warning disable CS8618
    private Shift() { } // Para EF Core
#pragma warning restore CS8618

    public Shift(Guid cashRegisterId, string userId, decimal initialCash)
    {
        if (cashRegisterId == Guid.Empty) throw new DomainException("El ID de caja es inválido.");
        if (string.IsNullOrWhiteSpace(userId)) throw new DomainException("El ID del usuario es requerido para abrir el turno.");
        if (initialCash < 0) throw new DomainException("El fondo inicial de caja no puede ser negativo.");

        CashRegisterId = cashRegisterId;
        UserId = userId;
        InitialCash = initialCash;
        StartTime = DateTime.UtcNow;
        Status = ShiftStatus.Open;
        
        IsGlobalInvoiceRequested = false;
        IsGlobalInvoiced = false;
        GlobalInvoiceId = null;
        IsConsolidated = false;

        SystemExpectedCash = 0;
        TotalCashReturns = 0;

        AddDomainEvent(new ShiftOpenedEvent(Id, CashRegisterId, UserId, InitialCash));
    }

    /// <summary>
    /// Cierra el turno registrando los acumulados de sistema.
    /// </summary>
    public void Close(
        DateTime endTime,
        decimal totalCashSales, 
        decimal totalCashReturns,
        decimal totalInflows, 
        decimal totalOutflows,
        IEnumerable<PaymentMethodBreakdown> paymentMethodTotals,
        IEnumerable<TaxBreakdown> salesTaxTotals,
        IEnumerable<TaxBreakdown> returnsTaxTotals)
    {
        if (Status == ShiftStatus.Closed) throw new DomainException("El turno ya se encuentra cerrado.");
        if (endTime < StartTime) throw new DomainException("La fecha-hora de cierre no puede ser anterior a la de apertura.");

        TotalCashReturns = totalCashReturns;
        SystemExpectedCash = InitialCash + totalCashSales + totalInflows - totalCashReturns - totalOutflows;
        
        EndTime = endTime;
        Status = ShiftStatus.Closed;

        if (paymentMethodTotals != null) _paymentMethodTotals.AddRange(paymentMethodTotals);
        if (salesTaxTotals != null) _salesTaxTotals.AddRange(salesTaxTotals);
        if (returnsTaxTotals != null) _returnsTaxTotals.AddRange(returnsTaxTotals);

        AddDomainEvent(new ShiftClosedEvent(Id, SystemExpectedCash));
    }

    public void RequestGlobalInvoice()
    {
        if (Status != ShiftStatus.Closed) throw new DomainException("Solo se puede solicitar la factura global de un turno cerrado.");
        if (IsGlobalInvoiced) throw new DomainException("El turno ya cuenta con una factura global generada.");
        if (IsGlobalInvoiceRequested) throw new DomainException("La factura global de este turno ya fue solicitada.");

        IsGlobalInvoiceRequested = true;
        AddDomainEvent(new ShiftGlobalInvoiceRequestedEvent(Id));
    }

    public void MarkAsGlobalInvoiced(string globalInvoiceId)
    {
        if (string.IsNullOrWhiteSpace(globalInvoiceId)) throw new DomainException("El ID de la factura global es requerido.");
        if (Status != ShiftStatus.Closed) throw new DomainException("Solo se puede facturar un turno cerrado.");
        if (IsGlobalInvoiced) throw new DomainException("El turno ya cuenta con una factura global generada.");

        IsGlobalInvoiced = true;
        GlobalInvoiceId = globalInvoiceId;
        AddDomainEvent(new ShiftGlobalInvoicedEvent(Id, globalInvoiceId));
    }

    public void RegisterCreditNote(string creditNoteId, decimal amount, string reason)
    {
        if (string.IsNullOrWhiteSpace(creditNoteId)) throw new DomainException("El ID de la nota de crédito es requerido.");
        if (amount <= 0) throw new DomainException("El monto de la nota de crédito debe ser mayor a cero.");
        if (string.IsNullOrWhiteSpace(reason)) throw new DomainException("Se requiere un motivo para la nota de crédito.");
        
        if (!IsGlobalInvoiced) throw new DomainException("No se pueden registrar notas de crédito a un turno que no ha sido facturado globalmente.");

        var creditNote = new ShiftCreditNote(creditNoteId, amount, reason, DateTime.UtcNow);
        _creditNotes.Add(creditNote);

        AddDomainEvent(new ShiftCreditNoteRegisteredEvent(Id, creditNoteId, amount));
    }

    public void Consolidate()
    {
        if (Status != ShiftStatus.Closed)
            throw new DomainException("Solo se pueden consolidar turnos que ya estén cerrados.");
        if (IsConsolidated)
            throw new DomainException("El turno ya se encuentra consolidado.");

        IsConsolidated = true;
        AddDomainEvent(new ShiftConsolidatedEvent(Id));
    }
}
