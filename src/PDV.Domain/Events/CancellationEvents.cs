using PDV.Domain.Enums;

namespace PDV.Domain.Events;

public record CancellationCreatedEvent(Guid CancellationId, CancellationType Type, Guid? SaleId, Guid? SaleItemId, string Reason) : IDomainEvent;
