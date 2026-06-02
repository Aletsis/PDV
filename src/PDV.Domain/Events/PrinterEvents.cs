using PDV.Domain.Enums;

namespace PDV.Domain.Events;

public record PrinterCreatedEvent(Guid PrinterId, string Name, PrinterConnectionType ConnectionType) : IDomainEvent;
public record PrinterUpdatedEvent(Guid PrinterId, string Name) : IDomainEvent;
public record PrinterActivatedEvent(Guid PrinterId) : IDomainEvent;
public record PrinterDeactivatedEvent(Guid PrinterId) : IDomainEvent;
