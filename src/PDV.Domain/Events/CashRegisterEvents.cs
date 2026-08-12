namespace PDV.Domain.Events;

public record CashRegisterCreatedEvent(Guid CashRegisterId, string Name, Guid BranchId) : IDomainEvent;
public record CashRegisterUserAssignedEvent(Guid CashRegisterId, string? UserId) : IDomainEvent;
public record CashRegisterPrinterAssignedEvent(Guid CashRegisterId, Guid? PrinterId) : IDomainEvent;
public record CashRegisterTicketSeriesConfiguredEvent(
    Guid CashRegisterId,
    string SaleSeries,
    string ReturnSeries,
    string OrderSeries) : IDomainEvent;
public record CashRegisterActivatedEvent(Guid CashRegisterId) : IDomainEvent;
public record CashRegisterDeactivatedEvent(Guid CashRegisterId) : IDomainEvent;
public record CashRegisterUpdatedEvent(Guid CashRegisterId, string Name) : IDomainEvent;

/// <summary>Se emite cuando una dirección IP es vinculada o desvinculada de la caja registradora.</summary>
public record CashRegisterIpBoundEvent(Guid CashRegisterId, string? IpAddress) : IDomainEvent;

/// <summary>Se emite cuando cambia el modo de operación de la caja registradora (Piso de Ventas / Pedidos).</summary>
public record CashRegisterModeChangedEvent(Guid CashRegisterId, PDV.Domain.Enums.CashRegisterMode Mode) : IDomainEvent;

