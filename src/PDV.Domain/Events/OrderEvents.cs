namespace PDV.Domain.Events;

public record OrderCreatedEvent(Guid OrderId, Guid? ClientId) : IDomainEvent;
public record OrderConfirmedEvent(Guid OrderId) : IDomainEvent;
public record OrderCancelledEvent(Guid OrderId, string Reason) : IDomainEvent;
public record OrderItemAddedEvent(Guid OrderId, Guid ProductId, decimal Quantity) : IDomainEvent;
public record OrderItemRemovedEvent(Guid OrderId, Guid ProductId) : IDomainEvent;
public record OrderAuthorizedEvent(Guid OrderId, string SupervisorId) : IDomainEvent;
public record OrderInvoiceRequestedEvent(Guid OrderId) : IDomainEvent;
public record OrderRoutedEvent(Guid OrderId, string RouteId, string RoutedById) : IDomainEvent;
public record OrderDeliveryAssignedEvent(Guid OrderId, string DeliveryManId) : IDomainEvent;
public record OrderDeliveredEvent(Guid OrderId) : IDomainEvent;
public record OrderReturnedEvent(Guid OrderId) : IDomainEvent;

