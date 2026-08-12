using System;
using PDV.Domain.Common;
using PDV.Domain.Enums;
using PDV.Domain.Exceptions;

namespace PDV.Domain.Entities;

/// <summary>
/// Mapeo de Conceptos de CONTPAQi Comercial por sucursal y tipo/subclasificación de movimiento de inventario,
/// incluyendo traspasos específicos entre sucursal origen y destino.
/// </summary>
public class InventoryConceptMapping : BaseEntity, IAggregateRoot
{
    /// <summary>Sucursal que posee la configuración (origen en traspasos o ejecutora del movimiento).</summary>
    public Guid BranchId { get; private set; }
    public Branch Branch { get; private set; } = null!;

    public InventoryMovementType MovementType { get; private set; }
    public InventoryMovementSubtype? Subtype { get; private set; }

    /// <summary>Sucursal destino requerida cuando MovementType == Transfer.</summary>
    public Guid? DestinationBranchId { get; private set; }
    public Branch? DestinationBranch { get; private set; }

    public string ConceptCode { get; private set; }
    public string ConceptName { get; private set; }
    public string? DefaultSeries { get; private set; }

#pragma warning disable CS8618
    private InventoryConceptMapping() { } // For EF Core
#pragma warning restore CS8618

    /// <summary>
    /// Constructor para compras, ajustes de entrada/salida o inventario inicial.
    /// </summary>
    public InventoryConceptMapping(
        Guid branchId,
        InventoryMovementType movementType,
        InventoryMovementSubtype? subtype,
        string conceptCode,
        string conceptName,
        string? defaultSeries = null)
    {
        if (branchId == Guid.Empty)
            throw new DomainException("La sucursal es obligatoria.");
        if (string.IsNullOrWhiteSpace(conceptCode))
            throw new DomainException("El código de concepto es obligatorio.");
        if (string.IsNullOrWhiteSpace(conceptName))
            throw new DomainException("El nombre del concepto es obligatorio.");

        Id = Guid.NewGuid();
        BranchId = branchId;
        MovementType = movementType;
        Subtype = subtype;
        DestinationBranchId = null;
        ConceptCode = conceptCode.Trim();
        ConceptName = conceptName.Trim();
        DefaultSeries = defaultSeries?.Trim();
    }

    /// <summary>
    /// Constructor para traspasos entre sucursales (origen -> destino).
    /// </summary>
    public InventoryConceptMapping(
        Guid branchId,
        Guid destinationBranchId,
        string conceptCode,
        string conceptName,
        string? defaultSeries = null,
        InventoryMovementSubtype? subtype = null)
    {
        if (branchId == Guid.Empty)
            throw new DomainException("La sucursal de origen es obligatoria.");
        if (destinationBranchId == Guid.Empty)
            throw new DomainException("La sucursal de destino es obligatoria.");
        if (branchId == destinationBranchId)
            throw new DomainException("La sucursal de origen y destino no pueden ser iguales.");
        if (string.IsNullOrWhiteSpace(conceptCode))
            throw new DomainException("El código de concepto es obligatorio.");
        if (string.IsNullOrWhiteSpace(conceptName))
            throw new DomainException("El nombre del concepto es obligatorio.");

        Id = Guid.NewGuid();
        BranchId = branchId;
        MovementType = InventoryMovementType.Transfer;
        DestinationBranchId = destinationBranchId;
        Subtype = subtype;
        ConceptCode = conceptCode.Trim();
        ConceptName = conceptName.Trim();
        DefaultSeries = defaultSeries?.Trim();
    }

    public void UpdateMapping(string conceptCode, string conceptName, string? defaultSeries = null)
    {
        if (string.IsNullOrWhiteSpace(conceptCode))
            throw new DomainException("El código de concepto es obligatorio.");
        if (string.IsNullOrWhiteSpace(conceptName))
            throw new DomainException("El nombre del concepto es obligatorio.");

        ConceptCode = conceptCode.Trim();
        ConceptName = conceptName.Trim();
        DefaultSeries = defaultSeries?.Trim();
    }
}
