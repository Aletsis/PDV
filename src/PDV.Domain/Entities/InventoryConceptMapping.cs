using System;
using PDV.Domain.Common;
using PDV.Domain.Enums;
using PDV.Domain.Exceptions;

namespace PDV.Domain.Entities;

/// <summary>
/// Mapeo de Conceptos de CONTPAQi Comercial por cada subclasificación de movimiento de inventario.
/// </summary>
public class InventoryConceptMapping : BaseEntity, IAggregateRoot
{
    public InventoryMovementSubtype Subtype { get; private set; }
    public string ConceptCode { get; private set; }
    public string ConceptName { get; private set; }
    public string? DefaultSeries { get; private set; }

#pragma warning disable CS8618
    private InventoryConceptMapping() { } // For EF Core
#pragma warning restore CS8618

    public InventoryConceptMapping(
        InventoryMovementSubtype subtype,
        string conceptCode,
        string conceptName,
        string? defaultSeries = null)
    {
        if (string.IsNullOrWhiteSpace(conceptCode))
            throw new DomainException("El código de concepto es obligatorio.");
        if (string.IsNullOrWhiteSpace(conceptName))
            throw new DomainException("El nombre del concepto es obligatorio.");

        Id = Guid.NewGuid();
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
