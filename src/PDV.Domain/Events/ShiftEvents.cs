namespace PDV.Domain.Events;

public record ShiftOpenedEvent(Guid ShiftId, Guid CashRegisterId, string UserId, decimal InitialCash) : IDomainEvent;
public record ShiftClosedEvent(Guid ShiftId, decimal SystemExpectedCash) : IDomainEvent;
public record ShiftGlobalInvoiceRequestedEvent(Guid ShiftId) : IDomainEvent;
public record ShiftGlobalInvoicedEvent(Guid ShiftId, string GlobalInvoiceId) : IDomainEvent;
public record ShiftCreditNoteRegisteredEvent(Guid ShiftId, string CreditNoteId, decimal Amount) : IDomainEvent;
public record ShiftConsolidatedEvent(Guid ShiftId) : IDomainEvent;

