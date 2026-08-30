using System;
using PDV.Domain.Common;
using PDV.Domain.Enums;

namespace PDV.Domain.Events;

public record DriverStatusChangedEvent(
    string UserId,
    Guid BranchId,
    PickerAvailabilityStatus NewStatus,
    string? Notes
) : IDomainEvent;
