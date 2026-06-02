using PDV.Domain.Enums;

namespace PDV.Domain.Events;

/// <summary>Se emite cada vez que se consume un número de nota de caja.</summary>
public record TicketIssuedEvent(
    Guid TicketSequenceId,
    Guid CashRegisterId,
    TicketSequenceType SequenceType,
    int IssuedTicketNumber) : IDomainEvent;

public record TicketSequenceCreatedEvent(
    Guid TicketSequenceId,
    Guid CashRegisterId,
    TicketSequenceType SequenceType) : IDomainEvent;
