using System;
using System.Collections.Generic;
using PDV.Domain.Enums;

namespace PDV.Application.Features.InventoryDocuments.Dtos;

public class InventoryDocumentDto
{
    public Guid Id { get; set; }
    public Guid BranchId { get; set; }
    public string BranchName { get; set; } = string.Empty;
    public Guid? DestinationBranchId { get; set; }
    public string? DestinationBranchName { get; set; }
    public InventoryMovementType Type { get; set; }
    public InventoryMovementSubtype Subtype { get; set; }
    public string Series { get; set; } = string.Empty;
    public string Folio { get; set; } = string.Empty;
    public Guid? SupplierId { get; set; }
    public string? SupplierCode { get; set; }
    public string? SupplierName { get; set; }
    public string? Reference { get; set; }
    public string? Remarks { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime Date { get; set; }
    public OutboxState SyncStatus { get; set; }
    public int Attempts { get; set; }
    public DateTime? LastAttemptAt { get; set; }
    public string? SyncErrorMessage { get; set; }
    public int? ExternalDocumentId { get; set; }
    public string? ExternalSeries { get; set; }
    public string? ExternalFolio { get; set; }
    public decimal TotalUnits { get; set; }
    public decimal TotalAmount { get; set; }
    public List<InventoryDocumentItemDto> Items { get; set; } = new();
}

public class InventoryDocumentItemDto
{
    public Guid Id { get; set; }
    public Guid ProductId { get; set; }
    public string ProductCode { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public string? UnitName { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitCost { get; set; }
    public decimal TotalCost => Quantity * UnitCost;
    public string? Remarks { get; set; }
}

public class InventoryConceptMappingDto
{
    public Guid Id { get; set; }
    public InventoryMovementSubtype Subtype { get; set; }
    public string SubtypeName { get; set; } = string.Empty;
    public string ConceptCode { get; set; } = string.Empty;
    public string ConceptName { get; set; } = string.Empty;
    public string? DefaultSeries { get; set; }
}
