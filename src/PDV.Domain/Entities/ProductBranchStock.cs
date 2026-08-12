using System;
using PDV.Domain.Common;
using PDV.Domain.Enums;
using PDV.Domain.Events;
using PDV.Domain.Exceptions;

namespace PDV.Domain.Entities;

/// <summary>
/// Entidad de dominio que representa las existencias y stock mínimo de un producto en una sucursal específica.
/// </summary>
public class ProductBranchStock : BaseEntity, IAggregateRoot
{
    public Guid ProductId { get; private set; }
    public Product Product { get; private set; } = null!;

    public Guid BranchId { get; private set; }
    public Branch Branch { get; private set; } = null!;

    public decimal Stock { get; private set; }      // decimal para soportar granel (kg/lt)
    public decimal MinStock { get; private set; }   // Stock mínimo para alerta de reorden

    public byte[]? RowVersion { get; set; }

#pragma warning disable CS8618
    private ProductBranchStock() { } // Para EF Core
#pragma warning restore CS8618

    public ProductBranchStock(Guid productId, Guid branchId, decimal stock = 0, decimal minStock = 0)
    {
        if (productId == Guid.Empty) throw new DomainException("El ID de producto es requerido.");
        if (branchId == Guid.Empty) throw new DomainException("El ID de sucursal es requerido.");
        if (stock < 0) throw new DomainException("El stock inicial no puede ser negativo.");
        if (minStock < 0) throw new DomainException("El stock mínimo no puede ser negativo.");

        ProductId = productId;
        BranchId = branchId;
        Stock = stock;
        MinStock = minStock;
    }

    public void ReduceStock(decimal quantity)
    {
        if (quantity <= 0) throw new DomainException("La cantidad a reducir debe ser mayor a cero.");
        if (Stock < quantity) throw new DomainException($"Stock insuficiente. Disponible: {Stock}, Requerido: {quantity}.");

        Stock -= quantity;
    }

    public void IncreaseStock(decimal quantity)
    {
        if (quantity <= 0) throw new DomainException("La cantidad a aumentar debe ser mayor a cero.");

        Stock += quantity;
    }

    public void AdjustStock(decimal newStock)
    {
        if (newStock < 0) throw new DomainException("El stock ajustado no puede ser negativo.");

        Stock = newStock;
    }

    public void UpdateMinStock(decimal newMinStock)
    {
        if (newMinStock < 0) throw new DomainException("El stock mínimo no puede ser negativo.");
        MinStock = newMinStock;
    }

    public bool HasStock(decimal quantity)
        => quantity > 0 && Stock >= quantity;

    public bool IsLowStock()
        => MinStock > 0 && Stock <= MinStock;

    public void ApplyMovement(
        decimal quantity, 
        InventoryMovementType type, 
        Guid? referenceId = null, 
        string? remarks = null,
        Guid? documentId = null,
        InventoryMovementSubtype? subtype = null)
    {
        if (quantity == 0)
            throw new DomainException("La cantidad del movimiento no puede ser cero.");

        Stock += quantity;

        // Registrar el movimiento de inventario en la sucursal
        var movementId = Guid.CreateVersion7();
        AddDomainEvent(new InventoryMovementRegisteredEvent(
            movementId, 
            ProductId, 
            BranchId, 
            quantity, 
            type, 
            referenceId, 
            remarks,
            documentId,
            subtype));
    }
}
