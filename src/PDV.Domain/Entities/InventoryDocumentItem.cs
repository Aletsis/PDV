using System;
using PDV.Domain.Common;
using PDV.Domain.Exceptions;

namespace PDV.Domain.Entities;

/// <summary>
/// Representa una partida dentro de un documento de inventario.
/// </summary>
public class InventoryDocumentItem : BaseEntity
{
    public Guid InventoryDocumentId { get; private set; }
    public InventoryDocument InventoryDocument { get; private set; } = null!;

    public Guid ProductId { get; private set; }
    public Product Product { get; private set; } = null!;

    public decimal Quantity { get; private set; }
    public decimal UnitCost { get; private set; }
    public string? Remarks { get; private set; }

#pragma warning disable CS8618
    private InventoryDocumentItem() { } // For EF Core
#pragma warning restore CS8618

    public InventoryDocumentItem(
        Guid inventoryDocumentId,
        Guid productId,
        decimal quantity,
        decimal unitCost = 0,
        string? remarks = null)
    {
        if (inventoryDocumentId == Guid.Empty)
            throw new DomainException("El ID del documento es obligatorio.");
        if (productId == Guid.Empty)
            throw new DomainException("El ID de producto es obligatorio.");
        if (quantity <= 0)
            throw new DomainException("La cantidad debe ser mayor a cero.");
        if (unitCost < 0)
            throw new DomainException("El costo unitario no puede ser negativo.");

        Id = Guid.NewGuid();
        InventoryDocumentId = inventoryDocumentId;
        ProductId = productId;
        Quantity = quantity;
        UnitCost = unitCost;
        Remarks = remarks?.Trim();
    }
}
