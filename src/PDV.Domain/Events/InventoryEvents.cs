using System;
using PDV.Domain.Common;
using PDV.Domain.Enums;

namespace PDV.Domain.Events;

public record InventoryMovementRegisteredEvent(
    Guid MovementId, 
    Guid ProductId, 
    Guid BranchId,
    decimal Quantity, 
    InventoryMovementType Type, 
    Guid? ReferenceId,
    string? Remarks = null,
    Guid? DocumentId = null,
    InventoryMovementSubtype? Subtype = null
) : IDomainEvent;
