using PDV.Domain.Enums;
using PDV.Domain.ValueObjects;

namespace PDV.Application.Features.Sales.Dtos;

public class CashDenominationDto
{
    public DenominationType Type { get; set; }
    public int Quantity { get; set; }
    public decimal UnitValue { get; set; }
    public decimal TotalValue => Quantity * UnitValue;
}

public class ShiftReconciliationItemDto
{
    public Guid ShiftId { get; set; }
    public Guid? CashCutId { get; set; }
    public Guid CashRegisterId { get; set; }
    public string CashRegisterName { get; set; } = string.Empty;
    public string CashierUserId { get; set; } = string.Empty;
    public string CashierName { get; set; } = string.Empty;
    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }

    public decimal InitialCash { get; set; }
    public decimal TotalSales { get; set; }
    public decimal CashSalesTotal { get; set; }
    public decimal CardSalesTotal { get; set; }
    public decimal InflowsTotal { get; set; }
    public decimal OutflowsTotal { get; set; }
    public decimal ReturnsTotal { get; set; }
    public decimal ExpectedCash { get; set; }
    public decimal ExpectedCardVouchers { get; set; }

    public bool IsReconciled { get; set; }
    public Guid? ReconciliationId { get; set; }
    public DateTime? ReconciliationDate { get; set; }
    public string? ReconciledByUserId { get; set; }
    public string? ReconciledByName { get; set; }
    public decimal? DeliveredCash { get; set; }
    public decimal? DeliveredCardVouchers { get; set; }
    public decimal? CashDifference { get; set; }
    public decimal? CardVouchersDifference { get; set; }
    public decimal? TotalDifference { get; set; }
    public ReconciliationStatus? Status { get; set; }
    public string? Notes { get; set; }
}

public class ReconciliationsSummaryDto
{
    public int TotalShifts { get; set; }
    public int TotalPending { get; set; }
    public int TotalReconciled { get; set; }
    public int TotalBalanced { get; set; }
    public int TotalWithDifference { get; set; }
    public decimal TotalNetDifference { get; set; }
    public List<ShiftReconciliationItemDto> Items { get; set; } = new();
}

public class ShiftMovementDetailDto
{
    public DateTime Date { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public decimal Amount { get; set; }
}

public class ShiftReconciliationDetailDto
{
    public Guid ShiftId { get; set; }
    public Guid? CashCutId { get; set; }
    public Guid CashRegisterId { get; set; }
    public string CashRegisterName { get; set; } = string.Empty;
    public string CashierUserId { get; set; } = string.Empty;
    public string CashierName { get; set; } = string.Empty;
    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }

    // Totales calculados de Sistema
    public decimal InitialCash { get; set; }
    public decimal TotalSales { get; set; }
    public decimal CashSalesTotal { get; set; }
    public decimal CardSalesTotal { get; set; }
    public decimal InflowsTotal { get; set; }
    public decimal OutflowsTotal { get; set; }
    public decimal ReturnsTotal { get; set; }
    public decimal ExpectedCash { get; set; }
    public decimal ExpectedCardVouchers { get; set; }

    // Desgloses
    public List<PaymentMethodBreakdown> PaymentMethods { get; set; } = new();
    public List<ShiftMovementDetailDto> InflowMovements { get; set; } = new();
    public List<ShiftMovementDetailDto> OutflowMovements { get; set; } = new();

    // Estado previo si ya fue conciliado
    public bool IsReconciled { get; set; }
    public Guid? ReconciliationId { get; set; }
    public DateTime? ReconciliationDate { get; set; }
    public string? ReconciledByUserId { get; set; }
    public string? ReconciledByName { get; set; }
    public decimal? DeliveredCash { get; set; }
    public decimal? DeliveredCardVouchers { get; set; }
    public decimal? CashDifference { get; set; }
    public decimal? CardVouchersDifference { get; set; }
    public decimal? TotalDifference { get; set; }
    public ReconciliationStatus? Status { get; set; }
    public string? Notes { get; set; }
    public List<CashDenominationDto> Denominations { get; set; } = new();
}

public class ReconciliationResultDto
{
    public Guid ReconciliationId { get; set; }
    public Guid ShiftId { get; set; }
    public ReconciliationStatus Status { get; set; }
    public decimal ExpectedCash { get; set; }
    public decimal DeliveredCash { get; set; }
    public decimal CashDifference { get; set; }
    public decimal ExpectedCardVouchers { get; set; }
    public decimal DeliveredCardVouchers { get; set; }
    public decimal CardVouchersDifference { get; set; }
    public decimal TotalDifference { get; set; }
    public bool IsBalanced => Status == ReconciliationStatus.Balanced;
    public string Message { get; set; } = string.Empty;
}
