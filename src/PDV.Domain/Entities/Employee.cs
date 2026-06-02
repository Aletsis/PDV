using PDV.Domain.Common;
using PDV.Domain.Enums;
using PDV.Domain.Events;
using PDV.Domain.Exceptions;

namespace PDV.Domain.Entities;

public class Employee : BaseEntity, IAggregateRoot
{
    public string Name { get; private set; }
    public string EmployeeCode { get; private set; } // ZKTeco ID or Nomina
    public EmployeeRole Role { get; private set; }
    public bool IsActive { get; private set; }
    public string? UserId { get; private set; } // Link to Identity User

#pragma warning disable CS8618
    private Employee() { } // Para EF Core
#pragma warning restore CS8618

    public Employee(string name, string employeeCode, EmployeeRole role, string? userId = null)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new DomainException("El nombre del empleado es requerido.");
        if (string.IsNullOrWhiteSpace(employeeCode)) throw new DomainException("El código de empleado (nómina/gafete) es requerido.");

        Name = name.Trim();
        EmployeeCode = employeeCode.Trim();
        Role = role;
        UserId = userId;
        IsActive = true;

        AddDomainEvent(new EmployeeCreatedEvent(Id, Name, EmployeeCode, Role));
    }

    public void Update(string name, string employeeCode, EmployeeRole role)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new DomainException("El nombre del empleado es requerido.");
        if (string.IsNullOrWhiteSpace(employeeCode)) throw new DomainException("El código de empleado (nómina/gafete) es requerido.");

        Name = name.Trim();
        EmployeeCode = employeeCode.Trim();
        Role = role;

        AddDomainEvent(new EmployeeUpdatedEvent(Id, Name, EmployeeCode, Role));
    }

    public void LinkUserId(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId)) throw new DomainException("El ID de usuario es inválido.");
        if (UserId == userId) return;

        UserId = userId;
        AddDomainEvent(new EmployeeUserIdLinkedEvent(Id, UserId));
    }

    public void Activate()
    {
        if (IsActive) throw new DomainException("El empleado ya se encuentra activo.");
        IsActive = true;
        AddDomainEvent(new EmployeeActivatedEvent(Id));
    }

    public void Deactivate()
    {
        if (!IsActive) throw new DomainException("El empleado ya se encuentra inactivo.");
        IsActive = false;
        AddDomainEvent(new EmployeeDeactivatedEvent(Id));
    }
}