using PDV.Domain.Common;
using PDV.Domain.Enums;
using PDV.Domain.Exceptions;

namespace PDV.Domain.Entities;

/// <summary>
/// Representa un mensaje de la cola de salida (Outbox Pattern) 
/// utilizado para garantizar la consistencia eventual y sincronización offline-first.
/// </summary>
public class OutboxMessage : BaseEntity
{
    public string EventType { get; private set; } = string.Empty;
    public string Payload { get; private set; } = string.Empty;
    public OutboxState State { get; private set; }
    public int Attempts { get; private set; }
    public DateTime? LastAttemptAt { get; private set; }
    public string? ErrorMessage { get; private set; }

#pragma warning disable CS8618
    private OutboxMessage() { } // Para EF Core
#pragma warning restore CS8618

    public OutboxMessage(string eventType, string payload)
    {
        if (string.IsNullOrWhiteSpace(eventType))
            throw new DomainException("El tipo de evento es requerido.");
        if (string.IsNullOrWhiteSpace(payload))
            throw new DomainException("El payload del mensaje no puede estar vacío.");

        Id = Guid.NewGuid(); // Generamos un Guid único secuencial (UUID v7/Guid)
        EventType = eventType.Trim();
        Payload = payload;
        State = OutboxState.Pending;
        Attempts = 0;
    }

    public void MarkAsProcessing()
    {
        if (State == OutboxState.Processed)
            throw new DomainException("No se puede procesar un mensaje que ya ha sido completado.");

        State = OutboxState.Processing;
        LastAttemptAt = DateTime.UtcNow;
    }

    public void MarkAsProcessed()
    {
        if (State != OutboxState.Processing && State != OutboxState.Pending)
            throw new DomainException($"Estado inválido para marcar como completado: '{State}'.");

        State = OutboxState.Processed;
        ErrorMessage = null;
    }

    /// <summary>
    /// Registra un intento fallido. Si se excede el límite de intentos, 
    /// el mensaje es enviado permanentemente a la Dead Letter Queue (DLQ / estado Failed).
    /// </summary>
    public void MarkAsFailed(string error, int maxAttempts)
    {
        if (string.IsNullOrWhiteSpace(error))
            throw new DomainException("El detalle del error es requerido para registrar la falla.");

        Attempts++;
        LastAttemptAt = DateTime.UtcNow;
        ErrorMessage = error;

        if (Attempts >= maxAttempts)
        {
            State = OutboxState.Failed; // Va a Dead Letter Queue lógicamente
        }
        else
        {
            State = OutboxState.Pending; // Se vuelve a poner en cola para reintentar
        }
    }
}
