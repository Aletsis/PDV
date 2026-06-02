using PDV.Domain.Enums;

namespace PDV.Domain.Events;

// Factura creada en sistema (aún sin timbrar)
public record InvoiceCreatedEvent(Guid InvoiceId, Guid BranchId, InvoiceType Type, Guid? SaleId, Guid? ShiftId, Guid? ReturnId = null) : IDomainEvent;


// Factura timbrada exitosamente (UUID recibido del PAC)
public record InvoiceStampedEvent(Guid InvoiceId, string Uuid) : IDomainEvent;

// Factura anulada solo en sistema (sin efecto ante el SAT)
public record InvoiceVoidedInSystemEvent(Guid InvoiceId, string Reason) : IDomainEvent;

// Factura cancelada formalmente ante el SAT
public record InvoiceCancelledAtSatEvent(
    Guid InvoiceId,
    string Uuid,
    SatCancellationMotif Motif,
    string? SubstituteUuid) : IDomainEvent;
