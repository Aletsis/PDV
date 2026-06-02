namespace PDV.Domain.Events;

public record ReturnRegisteredEvent(Guid ReturnId, Guid? SaleId, Guid? SaleItemId, decimal RefundAmount, string Reason) : IDomainEvent;
public record ReturnItemAddedEvent(Guid ReturnId, Guid ProductId, decimal Quantity) : IDomainEvent;
public record ReturnItemRemovedEvent(Guid ReturnId, Guid ProductId) : IDomainEvent;
public record ReturnCompletedEvent(Guid ReturnId, Guid? SaleId, decimal TotalRefund, PDV.Domain.Enums.RefundMethod RefundMethod) : IDomainEvent;
