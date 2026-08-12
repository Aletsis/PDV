using System;
using System.Collections.Generic;
using PDV.Domain.Common;
using PDV.Domain.Enums;
using PDV.Domain.Exceptions;

namespace PDV.Domain.Entities;

/// <summary>
/// Agregado raíz que representa un Documento de Inventario (Compras, Traspasos, Ajustes, Inventario Inicial).
/// Contiene trazabilidad completa de folios, auditoría, partidas y estado de sincronización con CONTPAQi Comercial.
/// </summary>
public class InventoryDocument : BaseEntity, IAggregateRoot
{
    public Guid BranchId { get; private set; }
    public Branch Branch { get; private set; } = null!;

    public Guid? DestinationBranchId { get; private set; }
    public Branch? DestinationBranch { get; private set; }

    public InventoryMovementType Type { get; private set; }
    public InventoryMovementSubtype Subtype { get; private set; }

    /// <summary>Serie asignada por el proveedor o por el concepto en CONTPAQi.</summary>
    public string Series { get; private set; } = string.Empty;

    /// <summary>Folio consecutivo o número asignado por CONTPAQi.</summary>
    public string Folio { get; private set; } = string.Empty;

    public Guid? SupplierId { get; private set; }
    public Supplier? Supplier { get; private set; }
    public string? SupplierCode { get; private set; }
    public string? SupplierName { get; private set; }

    public string? Reference { get; private set; }
    public string? Remarks { get; private set; }
    public DateTime Date { get; private set; }

    // Sincronización con CONTPAQi Comercial
    public OutboxState SyncStatus { get; private set; }
    public int Attempts { get; private set; }
    public DateTime? LastAttemptAt { get; private set; }
    public string? SyncErrorMessage { get; private set; }
    public int? ExternalDocumentId { get; private set; }
    public string? ExternalSeries { get; private set; }
    public string? ExternalFolio { get; private set; }

    private readonly List<InventoryDocumentItem> _items = new();
    public IReadOnlyCollection<InventoryDocumentItem> Items => _items.AsReadOnly();

#pragma warning disable CS8618
    private InventoryDocument() { } // For EF Core
#pragma warning restore CS8618

    public InventoryDocument(
        Guid branchId,
        InventoryMovementType type,
        InventoryMovementSubtype subtype,
        string createdBy,
        Guid? destinationBranchId = null,
        Guid? supplierId = null,
        string? supplierCode = null,
        string? supplierName = null,
        string? series = null,
        string? folio = null,
        string? reference = null,
        string? remarks = null)
    {
        if (branchId == Guid.Empty)
            throw new DomainException("La sucursal de origen es obligatoria.");
        if (string.IsNullOrWhiteSpace(createdBy))
            throw new DomainException("El usuario creador es obligatorio.");

        if (type == InventoryMovementType.Transfer)
        {
            if (destinationBranchId == null || destinationBranchId == Guid.Empty)
                throw new DomainException("La sucursal de destino es obligatoria para traspasos.");
            if (branchId == destinationBranchId)
                throw new DomainException("La sucursal de origen y destino no pueden ser iguales.");
        }

        if (type == InventoryMovementType.Purchase && supplierId == null && string.IsNullOrWhiteSpace(supplierCode))
        {
            throw new DomainException("El proveedor es requerido para registrar compras.");
        }

        Id = Guid.NewGuid();
        BranchId = branchId;
        DestinationBranchId = destinationBranchId;
        Type = type;
        Subtype = subtype;
        SetCreationAudit(createdBy.Trim());
        SupplierId = supplierId;
        SupplierCode = supplierCode?.Trim();
        SupplierName = supplierName?.Trim();
        Series = series?.Trim() ?? string.Empty;
        Folio = folio?.Trim() ?? string.Empty;
        Reference = reference?.Trim();
        Remarks = remarks?.Trim();
        Date = DateTime.UtcNow;
        SyncStatus = OutboxState.Pending;
        Attempts = 0;
    }

    public void AddItem(Guid productId, decimal quantity, decimal unitCost = 0, string? remarks = null)
    {
        if (productId == Guid.Empty)
            throw new DomainException("El ID de producto es obligatorio.");
        if (quantity <= 0)
            throw new DomainException("La cantidad debe ser mayor a cero.");

        var item = new InventoryDocumentItem(Id, productId, quantity, unitCost, remarks);
        _items.Add(item);
    }

    public void MarkAsSynced(int externalDocumentId, string series, string folio)
    {
        ExternalDocumentId = externalDocumentId;
        ExternalSeries = series?.Trim() ?? string.Empty;
        ExternalFolio = folio?.Trim() ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(series)) Series = ExternalSeries;
        if (!string.IsNullOrWhiteSpace(folio)) Folio = ExternalFolio;
        SyncStatus = OutboxState.Processed;
        SyncErrorMessage = null;
        LastAttemptAt = DateTime.UtcNow;
    }

    public void MarkAsSyncFailed(string error, int maxAttempts = 5)
    {
        Attempts++;
        LastAttemptAt = DateTime.UtcNow;
        SyncErrorMessage = error;

        if (Attempts >= maxAttempts)
        {
            SyncStatus = OutboxState.Failed;
        }
        else
        {
            SyncStatus = OutboxState.Pending;
        }
    }

    public void ResetSyncAttempts()
    {
        Attempts = 0;
        SyncStatus = OutboxState.Pending;
        SyncErrorMessage = null;
    }
}
