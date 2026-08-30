using System;
using PDV.Domain.Common;
using PDV.Domain.Enums;
using PDV.Domain.Exceptions;

namespace PDV.Domain.Entities;

/// <summary>
/// Representa una tarea encolada para la sincronización diferida (asíncrona) 
/// de clientes y productos hacia el ERP CONTPAQi Comercial.
/// </summary>
public class ContpaqiSyncQueue : BaseEntity
{
    public Guid ReferenceId { get; private set; }
    public string Type { get; private set; } = string.Empty; // "Client" o "Product"
    public string Action { get; private set; } = string.Empty; // "Create" o "Update"
    public OutboxState State { get; private set; }
    public int Attempts { get; private set; }
    public DateTime? LastAttemptAt { get; private set; }
    public string? ErrorMessage { get; private set; }

#pragma warning disable CS8618
    private ContpaqiSyncQueue() { } // Para EF Core
#pragma warning restore CS8618

    public ContpaqiSyncQueue(Guid referenceId, string type, string action)
    {
        if (referenceId == Guid.Empty)
            throw new DomainException("El ID de referencia no puede estar vacío.");
        if (string.IsNullOrWhiteSpace(type))
            throw new DomainException("El tipo de entidad es requerido.");
        if (string.IsNullOrWhiteSpace(action))
            throw new DomainException("La acción es requerida.");

        Id = Guid.NewGuid();
        ReferenceId = referenceId;
        Type = type.Trim();
        Action = action.Trim();
        State = OutboxState.Pending;
        Attempts = 0;
    }

    public void MarkAsProcessing()
    {
        if (State == OutboxState.Processed)
            throw new DomainException("No se puede procesar una tarea de CONTPAQi que ya ha sido completada.");

        State = OutboxState.Processing;
        LastAttemptAt = DateTime.Now;
    }

    public void MarkAsProcessed()
    {
        if (State != OutboxState.Processing && State != OutboxState.Pending)
            throw new DomainException($"Estado inválido para marcar como completado: '{State}'.");

        State = OutboxState.Processed;
        ErrorMessage = null;
    }

    public void MarkAsFailed(string error, int maxAttempts)
    {
        if (string.IsNullOrWhiteSpace(error))
            throw new DomainException("El detalle del error es requerido para registrar la falla.");

        Attempts++;
        LastAttemptAt = DateTime.Now;
        ErrorMessage = error;

        if (Attempts >= maxAttempts)
        {
            State = OutboxState.Failed;
        }
        else
        {
            State = OutboxState.Pending;
        }
    }
}
