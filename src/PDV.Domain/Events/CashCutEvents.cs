namespace PDV.Domain.Events;

public record CashCutCreatedEvent(Guid CashCutId, Guid ShiftId, decimal SystemExpectedCash, decimal DeclaredCash, decimal Difference) : IDomainEvent;
