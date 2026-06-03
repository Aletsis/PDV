using System;
using PDV.Domain.Common;

namespace PDV.Domain.Entities;

public class Category : BaseEntity, IAggregateRoot
{
    public string Name { get; private set; } = null!;
    public string? Description { get; private set; }
    public int? ClasificacionId { get; private set; } // ID de clasificación de CONTPAQi Comercial
    public bool IsActive { get; private set; }

    private Category() { } // EF Core

    public Category(string name, string? description = null, int? clasificacionId = null)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("El nombre de la categoría es requerido.");
        Name = name.Trim();
        Description = description?.Trim();
        ClasificacionId = clasificacionId;
        IsActive = true;
    }

    public void Update(string name, string? description = null, int? clasificacionId = null)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("El nombre de la categoría es requerido.");
        Name = name.Trim();
        Description = description?.Trim();
        ClasificacionId = clasificacionId;
    }

    public void Deactivate() => IsActive = false;
    public void Activate() => IsActive = true;
}
