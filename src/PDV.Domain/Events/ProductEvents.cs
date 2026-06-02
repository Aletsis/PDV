namespace PDV.Domain.Events;

public record ProductCreatedEvent(Guid ProductId, string Name, string Code) : IDomainEvent;
public record ProductInfoUpdatedEvent(Guid ProductId, string Name, string Category) : IDomainEvent;
public record ProductPriceUpdatedEvent(Guid ProductId, decimal OldPrice, decimal NewPrice) : IDomainEvent;
public record ProductStockReducedEvent(Guid ProductId, int Quantity, int RemainingStock) : IDomainEvent;
public record ProductStockIncreasedEvent(Guid ProductId, int Quantity, int NewStock) : IDomainEvent;
public record ProductStockAdjustedEvent(Guid ProductId, int OldStock, int NewStock) : IDomainEvent;
public record ProductActivatedEvent(Guid ProductId) : IDomainEvent;
public record ProductDeactivatedEvent(Guid ProductId) : IDomainEvent;
public record ProductCodeChangedEvent(Guid ProductId, string OldCode, string NewCode) : IDomainEvent;
