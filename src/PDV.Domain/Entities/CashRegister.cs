using System.Net;
using PDV.Domain.Common;
using PDV.Domain.Enums;
using PDV.Domain.Events;
using PDV.Domain.Exceptions;

namespace PDV.Domain.Entities;

public class CashRegister : BaseEntity, IAggregateRoot
{
    public string Name { get; private set; }
    public string Location { get; private set; }
    public bool IsActive { get; private set; }

    /// <summary>Dirección IP a la que está vinculada esta caja. Null = sin vincular.</summary>
    public string? IpAddress { get; private set; }

    /// <summary>Define si la caja opera en piso de ventas o como caja de pedidos.</summary>
    public CashRegisterMode Mode { get; private set; }

    public Guid BranchId { get; private set; }
    public Branch? Branch { get; private set; }

    public Guid? AssignedEmployeeId { get; private set; }
    public Employee? AssignedEmployee { get; private set; }

    public Guid? AssignedPrinterId { get; private set; }
    public Printer? AssignedPrinter { get; private set; }

#pragma warning disable CS8618
    private CashRegister() { } // Para EF Core
#pragma warning restore CS8618

    public CashRegister(
        string name,
        string location,
        Guid branchId,
        CashRegisterMode mode = CashRegisterMode.SalesFloor)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new DomainException("El nombre de la caja registradora es requerido.");
        if (branchId == Guid.Empty) throw new DomainException("El ID de sucursal es inválido.");

        Name = name.Trim();
        Location = location?.Trim() ?? string.Empty;
        BranchId = branchId;
        Mode = mode;
        IsActive = true;

        AddDomainEvent(new CashRegisterCreatedEvent(Id, Name, BranchId));
    }

    public void Update(string name, string location)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new DomainException("El nombre de la caja registradora es requerido.");
        Name = name.Trim();
        Location = location?.Trim() ?? string.Empty;

        AddDomainEvent(new CashRegisterUpdatedEvent(Id, Name));
    }

    // ──────────────────────────────────────────────
    // Vinculación de IP
    // ──────────────────────────────────────────────

    /// <summary>
    /// Vincula (o desvincula) esta caja a una dirección IP.
    /// La unicidad de IP entre cajas es responsabilidad del handler de aplicación.
    /// </summary>
    /// <param name="ipAddress">Dirección IPv4 o IPv6. Null para desvincular.</param>
    public void BindToIp(string? ipAddress)
    {
        if (ipAddress is not null)
        {
            var trimmed = ipAddress.Trim();
            if (!IPAddress.TryParse(trimmed, out _))
                throw new DomainException($"La dirección IP '{trimmed}' no es válida.");

            IpAddress = trimmed;
        }
        else
        {
            IpAddress = null;
        }

        AddDomainEvent(new CashRegisterIpBoundEvent(Id, IpAddress));
    }

    // ──────────────────────────────────────────────
    // Asignaciones
    // ──────────────────────────────────────────────

    public void AssignEmployee(Guid? employeeId)
    {
        AssignedEmployeeId = employeeId;
        AddDomainEvent(new CashRegisterEmployeeAssignedEvent(Id, employeeId));
    }

    public void AssignPrinter(Guid? printerId)
    {
        AssignedPrinterId = printerId;
        AddDomainEvent(new CashRegisterPrinterAssignedEvent(Id, printerId));
    }

    // ──────────────────────────────────────────────
    // Estado
    // ──────────────────────────────────────────────

    public void Activate()
    {
        if (IsActive) throw new DomainException("La caja ya está activa.");
        IsActive = true;
        AddDomainEvent(new CashRegisterActivatedEvent(Id));
    }

    public void Deactivate()
    {
        if (!IsActive) throw new DomainException("La caja ya está inactiva.");
        IsActive = false;
        AddDomainEvent(new CashRegisterDeactivatedEvent(Id));
    }
}

