namespace PDV.Domain.Events;

public record CashCollectedEvent(Guid CashCollectionId, Guid ShiftId, decimal Amount, string Reason) : IDomainEvent;
