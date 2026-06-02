using PDV.Domain.Common;
using PDV.Domain.Events;
using PDV.Domain.Exceptions;
using PDV.Domain.ValueObjects;

namespace PDV.Domain.Entities;

/// <summary>
/// Entidad que representa la consolidación física del corte de caja (lo que entrega el cajero)
/// </summary>
public class CashCut : BaseEntity, IAggregateRoot
{
    private readonly List<CashDenomination> _cashDenominations = new();
    private readonly List<PaymentMethodBreakdown> _declaredVouchers = new();

    public DateTime CutDate { get; private set; }
    
    public Guid ShiftId { get; private set; }
    public Shift? Shift { get; private set; }

    public string UserId { get; private set; }
    
    public Guid? EmployeeId { get; private set; }
    public Employee? Employee { get; private set; }
    
    public Guid CashRegisterId { get; private set; }
    public CashRegister? CashRegister { get; private set; }

    public decimal SystemExpectedCash { get; private set; }
    public decimal DeclaredPhysicalCash { get; private set; }
    public decimal DeclaredVouchersTotal { get; private set; }
    
    public decimal TotalDeclared => DeclaredPhysicalCash + DeclaredVouchersTotal;
    
    /// <summary>
    /// Diferencia entre el total declarado (físico + vouchers) y el efectivo esperado por el sistema.
    /// Negativo = Faltante, Positivo = Sobrante.
    /// </summary>
    public decimal Difference { get; private set; }

    public IReadOnlyCollection<CashDenomination> CashDenominations => _cashDenominations.AsReadOnly();
    public IReadOnlyCollection<PaymentMethodBreakdown> DeclaredVouchers => _declaredVouchers.AsReadOnly();

#pragma warning disable CS8618
    private CashCut() { } // Para EF Core
#pragma warning restore CS8618

    public CashCut(
        Guid shiftId,
        Guid cashRegisterId,
        string userId,
        decimal systemExpectedCash,
        IEnumerable<CashDenomination> cashDenominations,
        IEnumerable<PaymentMethodBreakdown> declaredVouchers,
        Guid? employeeId = null)
    {
        if (shiftId == Guid.Empty) throw new DomainException("El ID de turno es requerido.");
        if (cashRegisterId == Guid.Empty) throw new DomainException("El ID de caja es requerido.");
        if (string.IsNullOrWhiteSpace(userId)) throw new DomainException("El ID de usuario es requerido.");

        var cashDenominationsList = cashDenominations?.ToList() ?? new List<CashDenomination>();
        var declaredVouchersList = declaredVouchers?.ToList() ?? new List<PaymentMethodBreakdown>();

        ShiftId = shiftId;
        CashRegisterId = cashRegisterId;
        UserId = userId;
        EmployeeId = employeeId;
        SystemExpectedCash = systemExpectedCash;
        CutDate = DateTime.UtcNow;

        _cashDenominations.AddRange(cashDenominationsList);
        _declaredVouchers.AddRange(declaredVouchersList);

        DeclaredPhysicalCash = _cashDenominations.Sum(d => d.TotalValue);
        DeclaredVouchersTotal = _declaredVouchers.Sum(v => v.Amount);
        
        // La diferencia solo considera el efectivo esperado contra el efectivo físico declarado
        // Si el sistema esperaba vouchers y queremos cuadrar contra eso,
        // necesitamos SystemExpectedVouchers. Por simplicidad, asumimos que "SystemExpectedCash"
        // es literalmente el CASH esperado, por lo que la diferencia es:
        Difference = DeclaredPhysicalCash - SystemExpectedCash;

        AddDomainEvent(new CashCutCreatedEvent(Id, ShiftId, SystemExpectedCash, DeclaredPhysicalCash, Difference));
    }
}