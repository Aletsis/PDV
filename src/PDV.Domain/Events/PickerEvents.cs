using System;
using PDV.Domain.Common;
using PDV.Domain.Enums;

namespace PDV.Domain.Events;

public record PickerStatusChangedEvent(
    string UserId,
    Guid BranchId,
    PickerAvailabilityStatus NewStatus,
    string? Notes
) : IDomainEvent;

public record OrderAutoAssignedToPickerEvent(
    Guid OrderId,
    string PickerId,
    Guid BranchId
) : IDomainEvent;
