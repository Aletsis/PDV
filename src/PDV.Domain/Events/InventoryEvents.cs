using System;
using PDV.Domain.Common;
using PDV.Domain.Enums;

namespace PDV.Domain.Events;

public record InventoryMovementRegisteredEvent(
    Guid MovementId, 
    Guid ProductId, 
    decimal Quantity, 
    InventoryMovementType Type, 
    Guid? ReferenceId,
    string? Remarks = null
) : IDomainEvent;
