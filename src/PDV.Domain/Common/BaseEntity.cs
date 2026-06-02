using System.ComponentModel.DataAnnotations.Schema;
using PDV.Domain.Events;

namespace PDV.Domain.Common;

public abstract class BaseEntity
{
    // ──────────────────────────────────────────────
    // Identity
    // ──────────────────────────────────────────────
    public Guid Id { get; protected set; }

    public void SetId(Guid id)
    {
        Id = id;
    }

    // ──────────────────────────────────────────────
    // Auditoría de Creación — INMUTABLE
    // Solo la infraestructura puede asignarlos una única vez.
    // ──────────────────────────────────────────────
    public DateTime CreatedAt { get; private set; }
    public string? CreatedBy { get; private set; }

    // ──────────────────────────────────────────────
    // Auditoría de Modificación
    // ──────────────────────────────────────────────
    public DateTime? LastModifiedAt { get; private set; }
    public string? LastModifiedBy { get; private set; }

    // ──────────────────────────────────────────────
    // Soft Delete
    // ──────────────────────────────────────────────
    public bool IsDeleted { get; private set; }
    public DateTime? DeletedAt { get; private set; }
    public string? DeletedBy { get; private set; }

    // ──────────────────────────────────────────────
    // Domain Events
    // ──────────────────────────────────────────────
    private readonly List<IDomainEvent> _domainEvents = new();

    [NotMapped]
    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    protected BaseEntity()
    {
        Id = Guid.CreateVersion7();
        // CreatedAt se fija en el momento de instanciación — nunca cambia.
        CreatedAt = DateTime.UtcNow;
    }

    // ──────────────────────────────────────────────
    // Métodos de Auditoría — Solo para la capa de Infraestructura (SaveChanges interceptor)
    // ──────────────────────────────────────────────

    /// <summary>
    /// Establece el autor de creación. Solo puede llamarse una vez.
    /// Invocado por el interceptor de EF Core en el primer SaveChanges.
    /// </summary>
    public void SetCreationAudit(string createdBy)
    {
        if (CreatedBy is not null) return; // Inmutable: ya fue asignado, se ignora.
        CreatedBy = createdBy;
    }

    /// <summary>
    /// Actualiza los campos de última modificación.
    /// Invocado por el interceptor de EF Core en cada SaveChanges posterior.
    /// </summary>
    public void SetModificationAudit(string modifiedBy)
    {
        LastModifiedAt = DateTime.UtcNow;
        LastModifiedBy = modifiedBy;
    }

    // ──────────────────────────────────────────────
    // Soft Delete
    // ──────────────────────────────────────────────

    public void SoftDelete(string deletedBy)
    {
        if (IsDeleted) return; // Idempotente: ya eliminado, se ignora.

        IsDeleted = true;
        DeletedAt = DateTime.UtcNow;
        DeletedBy = deletedBy;
    }

    public void Restore()
    {
        if (!IsDeleted) return; // Idempotente: no estaba eliminado.

        IsDeleted = false;
        DeletedAt = null;
        DeletedBy = null;
    }

    // ──────────────────────────────────────────────
    // Domain Events API
    // ──────────────────────────────────────────────

    public void AddDomainEvent(IDomainEvent domainEvent)
    {
        _domainEvents.Add(domainEvent);
    }

    public void RemoveDomainEvent(IDomainEvent domainEvent)
    {
        _domainEvents.Remove(domainEvent);
    }

    public void ClearDomainEvents()
    {
        _domainEvents.Clear();
    }
}
