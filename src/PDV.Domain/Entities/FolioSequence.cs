using PDV.Domain.Common;
using PDV.Domain.Enums;
using PDV.Domain.Events;
using PDV.Domain.Exceptions;

namespace PDV.Domain.Entities;

/// <summary>
/// Agregado raíz que controla la secuencia de folios de CFDI por sucursal y tipo.
/// Un registro por combinación (Branch + InvoiceType).
/// 
/// CONCURRENCIA: El repositorio debe usar UPDLOCK (SQL Server) o SELECT FOR UPDATE
/// al leer este agregado para garantizar que solo una transacción a la vez
/// pueda obtener el siguiente folio y evitar duplicados fiscales.
/// </summary>
public class FolioSequence : BaseEntity, IAggregateRoot
{
    public Guid BranchId { get; private set; }
    public Branch? Branch { get; private set; }

    public InvoiceType SeriesType { get; private set; }

    /// <summary>Prefijo de la serie. Ej: "A", "G", "NC".</summary>
    public string Series { get; private set; }

    /// <summary>Código del concepto de facturación en CONTPAQi Comercial.</summary>
    public string? ConceptCode { get; private set; }

    /// <summary>Último folio emitido. Inicia en 0 (aún no se ha emitido ninguno).</summary>
    public int LastFolio { get; private set; }

    /// <summary>
    /// Longitud del número al formatear el folio completo.
    /// Default: 6 → "A000001". El SAT acepta hasta 8 dígitos.
    /// </summary>
    public int FolioDigits { get; private set; }

    /// <summary>Token de concurrencia optimista — detecta modificaciones concurrentes.</summary>
    public byte[]? RowVersion { get; set; }

#pragma warning disable CS8618
    private FolioSequence() { } // Para EF Core
#pragma warning restore CS8618

    public FolioSequence(Guid branchId, InvoiceType seriesType, string series, int folioDigits = 6)
    {
        if (branchId == Guid.Empty) throw new DomainException("El ID de sucursal es requerido.");
        if (string.IsNullOrWhiteSpace(series)) throw new DomainException("La serie es requerida.");
        if (folioDigits < 1 || folioDigits > 8) throw new DomainException("El número de dígitos del folio debe estar entre 1 y 8.");

        BranchId = branchId;
        SeriesType = seriesType;
        Series = series.Trim().ToUpperInvariant();
        LastFolio = 0;
        FolioDigits = folioDigits;

        AddDomainEvent(new FolioSequenceCreatedEvent(Id, BranchId, SeriesType, Series));
    }

    /// <summary>
    /// Obtiene e incrementa el siguiente folio de forma atómica.
    /// El repositorio DEBE haber adquirido un lock pesimista antes de llamar a este método.
    /// </summary>
    /// <returns>El número de folio asignado (entero).</returns>
    public int GetNextFolio()
    {
        LastFolio++;

        AddDomainEvent(new FolioIssuedEvent(
            Id,
            BranchId,
            SeriesType,
            Series,
            LastFolio,
            FormatFolio(LastFolio)));

        return LastFolio;
    }

    /// <summary>
    /// Devuelve el folio completo formateado: Serie + número con ceros a la izquierda.
    /// Ej: Series="A", LastFolio=1 → "A000001".
    /// </summary>
    public string GetFormattedLastFolio() => FormatFolio(LastFolio);

    /// <summary>
    /// Permite corregir la secuencia manualmente (ej. tras una migración o sincronización con el SAT).
    /// Solo debe usarse en contextos administrativos controlados.
    /// </summary>
    public void ResetTo(int folio)
    {
        if (folio < 0) throw new DomainException("El folio de reset no puede ser negativo.");
        LastFolio = folio;
    }

    /// <summary>
    /// Actualiza el concepto de facturación en CONTPAQi, la serie y el folio de forma manual.
    /// </summary>
    public void UpdateConcept(string? conceptCode, string series, int lastFolio)
    {
        ConceptCode = conceptCode?.Trim();
        Series = series.Trim().ToUpperInvariant();
        LastFolio = lastFolio;
    }

    private string FormatFolio(int folio) =>
        $"{Series}{folio.ToString().PadLeft(FolioDigits, '0')}";
}
