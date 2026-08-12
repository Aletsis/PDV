using PDV.Domain.Enums;

namespace PDV.Domain.Events;

public record CashCutCreatedEvent(Guid CashCutId, Guid ShiftId, decimal SystemExpectedCash, decimal DeclaredCash, decimal Difference) : IDomainEvent;

public record CashCutReconciledEvent(
    Guid ReconciliationId,
    Guid ShiftId,
    Guid? CashCutId,
    Guid CashRegisterId,
    string CashierUserId,
    string ReconciledByUserId,
    decimal ExpectedCash,
    decimal DeliveredCash,
    decimal CashDifference,
    decimal ExpectedCardVouchers,
    decimal DeliveredCardVouchers,
    decimal CardVouchersDifference,
    ReconciliationStatus Status
) : IDomainEvent;

