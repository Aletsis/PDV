using System;
using PDV.Domain.Enums;

namespace PDV.Application.Features.Pickers.Dtos;

public class PickerStatusDto
{
    public string UserId { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? EmployeeNumber { get; set; }
    public Guid BranchId { get; set; }
    public string BranchName { get; set; } = string.Empty;
    public PickerAvailabilityStatus Status { get; set; }
    public int? CustomMaxCapacity { get; set; }
    public int EffectiveMaxCapacity { get; set; }
    public int ActiveOrdersCount { get; set; }
    public int OrdersCompletedToday { get; set; }
    public DateTime LastStatusChangeAt { get; set; }
    public DateTime? LastAssignedOrderAt { get; set; }
    public string? StatusNotes { get; set; }
    public bool IsEligible => Status == PickerAvailabilityStatus.Available && ActiveOrdersCount < EffectiveMaxCapacity;
}
