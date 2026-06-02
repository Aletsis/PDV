namespace PDV.Domain.Events;

public record EmployeeCreatedEvent(Guid EmployeeId, string Name, string EmployeeCode, PDV.Domain.Enums.EmployeeRole Role) : IDomainEvent;
public record EmployeeUpdatedEvent(Guid EmployeeId, string Name, string EmployeeCode, PDV.Domain.Enums.EmployeeRole Role) : IDomainEvent;
public record EmployeeActivatedEvent(Guid EmployeeId) : IDomainEvent;
public record EmployeeDeactivatedEvent(Guid EmployeeId) : IDomainEvent;
public record EmployeeUserIdLinkedEvent(Guid EmployeeId, string UserId) : IDomainEvent;
