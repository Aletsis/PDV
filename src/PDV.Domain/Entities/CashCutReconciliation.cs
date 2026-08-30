using PDV.Domain.Common;
using PDV.Domain.Enums;
using PDV.Domain.Events;
using PDV.Domain.Exceptions;
using PDV.Domain.ValueObjects;

namespace PDV.Domain.Entities;

/// <summary>
/// Representa la validación y registro de conciliación física del corte de caja realizada por la encargada/supervisora.
/// </summary>
public class CashCutReconciliation : BaseEntity, IAggregateRoot
{
    private readonly List<CashDenomination> _cashDenominations = new();

    public Guid ShiftId { get; private set; }
    public Shift? Shift { get; private set; }

    public Guid? CashCutId { get; private set; }
    public CashCut? CashCut { get; private set; }

    public Guid CashRegisterId { get; private set; }
    public CashRegister? CashRegister { get; private set; }

    public string CashierUserId { get; private set; } = string.Empty;
    public string ReconciledByUserId { get; private set; } = string.Empty;

    public DateTime ReconciliationDate { get; private set; }

    // Totales calculados por el sistema
    public decimal InitialCash { get; private set; }
    public decimal CashSalesTotal { get; private set; }
    public decimal CardSalesTotal { get; private set; }
    public decimal InflowsTotal { get; private set; }
    public decimal OutflowsTotal { get; private set; }
    public decimal ReturnsTotal { get; private set; }
    public decimal ExpectedCash { get; private set; }
    public decimal ExpectedCardVouchers { get; private set; }

    // Valores entregados por la cajera a la encargada
    public decimal DeliveredCash { get; private set; }
    public decimal DeliveredCardVouchers { get; private set; }

    // Diferencias
    public decimal CashDifference { get; private set; }
    public decimal CardVouchersDifference { get; private set; }
    public decimal TotalDifference => CashDifference + CardVouchersDifference;

    public ReconciliationStatus Status { get; private set; }
    public string? Notes { get; private set; }

    public IReadOnlyCollection<CashDenomination> CashDenominations => _cashDenominations.AsReadOnly();

#pragma warning disable CS8618
    private CashCutReconciliation() { } // EF Core
#pragma warning restore CS8618

    public CashCutReconciliation(
        Guid shiftId,
        Guid? cashCutId,
        Guid cashRegisterId,
        string cashierUserId,
        string reconciledByUserId,
        decimal initialCash,
        decimal cashSalesTotal,
        decimal cardSalesTotal,
        decimal inflowsTotal,
        decimal outflowsTotal,
        decimal returnsTotal,
        decimal expectedCash,
        decimal expectedCardVouchers,
        decimal deliveredCash,
        decimal deliveredCardVouchers,
        string? notes = null,
        IEnumerable<CashDenomination>? denominations = null)
    {
        if (shiftId == Guid.Empty) throw new DomainException("El ID de turno es requerido para la conciliación.");
        if (cashRegisterId == Guid.Empty) throw new DomainException("El ID de caja es requerido para la conciliación.");
        if (string.IsNullOrWhiteSpace(cashierUserId)) throw new DomainException("El ID del cajero es requerido.");
        if (string.IsNullOrWhiteSpace(reconciledByUserId)) throw new DomainException("El ID del usuario supervisor/encargada es requerido.");

        ShiftId = shiftId;
        CashCutId = cashCutId;
        CashRegisterId = cashRegisterId;
        CashierUserId = cashierUserId;
        ReconciledByUserId = reconciledByUserId;
        ReconciliationDate = DateTime.Now;

        InitialCash = initialCash;
        CashSalesTotal = cashSalesTotal;
        CardSalesTotal = cardSalesTotal;
        InflowsTotal = inflowsTotal;
        OutflowsTotal = outflowsTotal;
        ReturnsTotal = returnsTotal;
        ExpectedCash = expectedCash;
        ExpectedCardVouchers = expectedCardVouchers;

        DeliveredCash = deliveredCash;
        DeliveredCardVouchers = deliveredCardVouchers;
        Notes = notes;

        CashDifference = DeliveredCash - ExpectedCash;
        CardVouchersDifference = DeliveredCardVouchers - ExpectedCardVouchers;

        Status = CalculateStatus(CashDifference, CardVouchersDifference);

        if (denominations != null)
        {
            _cashDenominations.AddRange(denominations);
        }

        AddDomainEvent(new CashCutReconciledEvent(
            Id,
            ShiftId,
            CashCutId,
            CashRegisterId,
            CashierUserId,
            ReconciledByUserId,
            ExpectedCash,
            DeliveredCash,
            CashDifference,
            ExpectedCardVouchers,
            DeliveredCardVouchers,
            CardVouchersDifference,
            Status
        ));
    }

    private static ReconciliationStatus CalculateStatus(decimal cashDiff, decimal voucherDiff)
    {
        bool cashOk = Math.Abs(cashDiff) < 0.001m;
        bool voucherOk = Math.Abs(voucherDiff) < 0.001m;

        if (cashOk && voucherOk) return ReconciliationStatus.Balanced;
        if (!cashOk && voucherOk)
        {
            return cashDiff < 0 ? ReconciliationStatus.CashShortage : ReconciliationStatus.CashSurplus;
        }
        if (cashOk && !voucherOk)
        {
            return voucherDiff < 0 ? ReconciliationStatus.VoucherShortage : ReconciliationStatus.VoucherSurplus;
        }

        return ReconciliationStatus.Discrepancy;
    }
}
