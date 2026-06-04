using PDV.Domain.Enums;

namespace PDV.Domain.Events;

public record SaleCreatedEvent(Guid SaleId, Guid? ClientId) : IDomainEvent;
public record SaleItemAddedEvent(Guid SaleId, Guid ProductId, decimal Quantity) : IDomainEvent;
public record SaleItemRemovedEvent(Guid SaleId, Guid ProductId) : IDomainEvent;
public record SalePaymentMadeEvent(Guid SaleId, decimal Amount, PaymentMethodType PaymentMethod) : IDomainEvent;
public record SaleCancelledEvent(Guid SaleId, string Reason) : IDomainEvent;
public record SaleInvoiceRequestedEvent(Guid SaleId) : IDomainEvent;
public record SaleInvoicedEvent(Guid SaleId, string InvoiceId) : IDomainEvent;
