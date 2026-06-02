using PDV.Domain.Enums;

namespace PDV.Domain.Events;

public record ClientRegisteredEvent(Guid ClientId, string Name) : IDomainEvent;
public record ClientActivatedEvent(Guid ClientId) : IDomainEvent;
public record ClientDeactivatedEvent(Guid ClientId) : IDomainEvent;
public record ClientContactInfoUpdatedEvent(Guid ClientId, string Phone, string Email) : IDomainEvent;
public record ClientAddressUpdatedEvent(Guid ClientId) : IDomainEvent;
public record ClientProfileUpdatedEvent(Guid ClientId, string Name, string TaxId) : IDomainEvent;
public record ClientTypeChangedEvent(Guid ClientId, ClientType NewType) : IDomainEvent;
