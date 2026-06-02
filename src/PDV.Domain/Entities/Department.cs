using System;
using PDV.Domain.Common;

namespace PDV.Domain.Entities;

public class Department : BaseEntity, IAggregateRoot
{
    public string Name { get; private set; }
    public string? Description { get; private set; }
    public int? ClasificacionId { get; private set; } // ID de clasificación 5 de CONTPAQi Comercial
    public bool IsActive { get; private set; }

    private Department() { } // EF Core

    public Department(string name, string? description = null, int? clasificacionId = null)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("El nombre del departamento es requerido.");
        Name = name.Trim();
        Description = description?.Trim();
        ClasificacionId = clasificacionId;
        IsActive = true;
    }

    public void Update(string name, string? description = null, int? clasificacionId = null)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("El nombre del departamento es requerido.");
        Name = name.Trim();
        Description = description?.Trim();
        ClasificacionId = clasificacionId;
    }

    public void Deactivate() => IsActive = false;
    public void Activate() => IsActive = true;
}
