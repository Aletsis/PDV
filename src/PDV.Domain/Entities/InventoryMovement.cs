using System;
using PDV.Domain.Common;
using PDV.Domain.Enums;
using PDV.Domain.Exceptions;

namespace PDV.Domain.Entities;

public class InventoryMovement : BaseEntity
{
    public Guid ProductId { get; private set; }
    public Product Product { get; private set; } = null!;
    
    public decimal Quantity { get; private set; }
    public InventoryMovementType Type { get; private set; }
    public DateTime Date { get; private set; }
    public Guid? ReferenceId { get; private set; }
    public string? Remarks { get; private set; }

#pragma warning disable CS8618
    private InventoryMovement() { } // For EF Core
#pragma warning restore CS8618

    public InventoryMovement(
        Guid productId,
        decimal quantity,
        InventoryMovementType type,
        Guid? referenceId = null,
        string? remarks = null)
    {
        if (productId == Guid.Empty)
            throw new DomainException("El ID de producto es requerido.");
        if (quantity == 0)
            throw new DomainException("La cantidad del movimiento de inventario no puede ser cero.");

        ProductId = productId;
        Quantity = quantity;
        Type = type;
        ReferenceId = referenceId;
        Remarks = remarks?.Trim();
        Date = DateTime.UtcNow;
    }
}
