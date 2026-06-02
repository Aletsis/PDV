using PDV.Domain.Enums;

namespace PDV.Domain.Events;

/// <summary>Se emite cada vez que se consume un folio, creando traza de auditoría.</summary>
public record FolioIssuedEvent(
    Guid FolioSequenceId,
    Guid BranchId,
    InvoiceType SeriesType,
    string Series,
    int IssuedFolio,
    string FormattedFolio) : IDomainEvent;

public record FolioSequenceCreatedEvent(
    Guid FolioSequenceId,
    Guid BranchId,
    InvoiceType SeriesType,
    string Series) : IDomainEvent;
