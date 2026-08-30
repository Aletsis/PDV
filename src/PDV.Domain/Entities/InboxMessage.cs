using System;
using PDV.Domain.Common;
using PDV.Domain.Enums;
using PDV.Domain.Exceptions;

namespace PDV.Domain.Entities;

public class InboxMessage : BaseEntity
{
    public Guid MessageId { get; private set; }
    public string EventType { get; private set; } = string.Empty;
    public string Payload { get; private set; } = string.Empty;
    public InboxState State { get; private set; }
    public int Attempts { get; private set; }
    public DateTime ReceivedAt { get; private set; }
    public DateTime? ProcessedAt { get; private set; }
    public string? ErrorMessage { get; private set; }

#pragma warning disable CS8618
    private InboxMessage() { } // EF Core
#pragma warning restore CS8618

    public InboxMessage(Guid messageId, string eventType, string payload)
    {
        if (messageId == Guid.Empty)
            throw new DomainException("El ID del mensaje original no puede estar vacío.");
        if (string.IsNullOrWhiteSpace(eventType))
            throw new DomainException("El tipo de evento es requerido.");
        if (string.IsNullOrWhiteSpace(payload))
            throw new DomainException("El payload no puede estar vacío.");

        MessageId = messageId;
        EventType = eventType.Trim();
        Payload = payload;
        State = InboxState.Pending;
        Attempts = 0;
        ReceivedAt = DateTime.Now;
    }

    public void MarkAsProcessing()
    {
        if (State == InboxState.Processed)
            throw new DomainException("No se puede procesar un mensaje que ya ha sido completado.");

        State = InboxState.Processing;
    }

    public void MarkAsProcessed()
    {
        State = InboxState.Processed;
        ProcessedAt = DateTime.Now;
        ErrorMessage = null;
    }

    public void MarkAsFailed(string error, int maxAttempts)
    {
        Attempts++;
        ErrorMessage = error;

        if (Attempts >= maxAttempts)
        {
            State = InboxState.Failed;
        }
        else
        {
            State = InboxState.Pending;
        }
    }
}
