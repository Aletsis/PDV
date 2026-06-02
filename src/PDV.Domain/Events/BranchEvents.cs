namespace PDV.Domain.Events;

public record BranchCreatedEvent(Guid BranchId, string Name, string Code) : IDomainEvent;
public record BranchUpdatedEvent(Guid BranchId, string Name) : IDomainEvent;
public record BranchActivatedEvent(Guid BranchId) : IDomainEvent;
public record BranchDeactivatedEvent(Guid BranchId) : IDomainEvent;
public record BranchSetAsMainEvent(Guid BranchId) : IDomainEvent;
public record BranchInvoiceSeriesConfiguredEvent(
    Guid BranchId,
    string InvoiceSeries,
    string GlobalInvoiceSeries,
    string CreditNoteSeries) : IDomainEvent;
