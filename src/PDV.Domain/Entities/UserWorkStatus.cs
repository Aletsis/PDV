using System;
using PDV.Domain.Common;
using PDV.Domain.Enums;
using PDV.Domain.Events;
using PDV.Domain.Exceptions;

namespace PDV.Domain.Entities;

public class UserWorkStatus : BaseEntity, IAggregateRoot
{
    public string UserId { get; private set; }
    public Guid BranchId { get; private set; }
    public Branch? Branch { get; private set; }

    public PickerAvailabilityStatus Status { get; private set; }
    
    /// <summary>
    /// Capacidad máxima de pedidos simultáneos configurada para este surtidor en específico.
    /// Si es null o <= 0, se utiliza el valor predeterminado del sistema (SystemConfiguration).
    /// </summary>
    public int? MaxConcurrentOrders { get; private set; }

    public DateTime LastStatusChangeAt { get; private set; }
    public DateTime? LastAssignedOrderAt { get; private set; }
    public int OrdersCompletedToday { get; private set; }
    public string? StatusNotes { get; private set; }

#pragma warning disable CS8618
    private UserWorkStatus() { } // For EF Core
#pragma warning restore CS8618

    public UserWorkStatus(string userId, Guid branchId, int? maxConcurrentOrders = null)
    {
        if (string.IsNullOrWhiteSpace(userId))
            throw new DomainException("El ID de usuario es requerido.");
        if (branchId == Guid.Empty)
            throw new DomainException("El ID de sucursal es requerido.");

        UserId = userId;
        BranchId = branchId;
        Status = PickerAvailabilityStatus.Available;
        MaxConcurrentOrders = maxConcurrentOrders > 0 ? maxConcurrentOrders : null;
        LastStatusChangeAt = DateTime.Now;
        LastAssignedOrderAt = null;
        OrdersCompletedToday = 0;
        StatusNotes = null;

        AddDomainEvent(new PickerStatusChangedEvent(UserId, BranchId, Status, StatusNotes));
    }

    public void SetAvailable()
    {
        Status = PickerAvailabilityStatus.Available;
        LastStatusChangeAt = DateTime.Now;
        StatusNotes = null;
        AddDomainEvent(new PickerStatusChangedEvent(UserId, BranchId, Status, StatusNotes));
    }

    public void SetMealBreak(string? notes = null)
    {
        Status = PickerAvailabilityStatus.MealBreak;
        LastStatusChangeAt = DateTime.Now;
        StatusNotes = notes?.Trim();
        AddDomainEvent(new PickerStatusChangedEvent(UserId, BranchId, Status, StatusNotes));
    }

    public void SetOperationalBreak(string? reason = null)
    {
        Status = PickerAvailabilityStatus.OperationalBreak;
        LastStatusChangeAt = DateTime.Now;
        StatusNotes = reason?.Trim();
        AddDomainEvent(new PickerStatusChangedEvent(UserId, BranchId, Status, StatusNotes));
    }

    public void SetOffDuty(string? notes = null)
    {
        Status = PickerAvailabilityStatus.OffDuty;
        LastStatusChangeAt = DateTime.Now;
        StatusNotes = notes?.Trim();
        AddDomainEvent(new PickerStatusChangedEvent(UserId, BranchId, Status, StatusNotes));
    }

    public void SetCustomCapacity(int? maxOrders)
    {
        if (maxOrders.HasValue && maxOrders.Value < 1)
            throw new DomainException("La capacidad máxima de pedidos debe ser al menos 1.");

        MaxConcurrentOrders = maxOrders;
    }

    public void RecordOrderAssigned()
    {
        LastAssignedOrderAt = DateTime.Now;
    }

    public void RecordOrderCompleted()
    {
        OrdersCompletedToday++;
    }

    public void ResetDailyCounters()
    {
        OrdersCompletedToday = 0;
    }

    public void ChangeBranch(Guid newBranchId)
    {
        if (newBranchId == Guid.Empty)
            throw new DomainException("El ID de sucursal es inválido.");

        BranchId = newBranchId;
        LastStatusChangeAt = DateTime.Now;
    }
}
