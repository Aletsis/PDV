using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using PDV.Domain.Common;
using PDV.Domain.Entities;
using PDV.Domain.Events;

namespace PDV.Infrastructure.Persistence.Interceptors;

/// <summary>
/// Interceptor de EF Core que procesa los eventos de dominio de las entidades rastreadas
/// justo antes de persistir cambios. Responsabilidades:
///   1. Crear y persistir <see cref="InventoryMovement"/> a partir de <see cref="InventoryMovementRegisteredEvent"/>.
///   2. Serializar todos los eventos como <see cref="OutboxMessage"/> para la sincronización offline.
///   3. Limpiar los eventos del agregado para evitar duplicidades.
/// </summary>
public sealed class DomainEventsInterceptor : SaveChangesInterceptor
{
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is not null)
            DispatchDomainEvents(eventData.Context);

        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        if (eventData.Context is not null)
            DispatchDomainEvents(eventData.Context);

        return base.SavingChanges(eventData, result);
    }

    // ──────────────────────────────────────────────────────────────────────
    // Implementación interna
    // ──────────────────────────────────────────────────────────────────────

    private static void DispatchDomainEvents(DbContext context)
    {
        // 1. Recolectar entidades con eventos pendientes
        var domainEntities = context.ChangeTracker
            .Entries<BaseEntity>()
            .Where(e => e.Entity.DomainEvents.Any())
            .ToList();

        var domainEvents = domainEntities
            .SelectMany(e => e.Entity.DomainEvents)
            .ToList();

        if (domainEvents.Count == 0) return;

        // 2. Procesar cada evento
        foreach (var domainEvent in domainEvents)
        {
            // 2a. Eventos especiales que requieren persistencia de entidad adicional
            HandleSpecialEvents(context, domainEvent);

            // 2b. Todo evento se registra en el Outbox para sincronización offline
            var eventType = domainEvent.GetType().Name;
            var payload = System.Text.Json.JsonSerializer.Serialize(domainEvent, domainEvent.GetType());
            context.Set<OutboxMessage>().Add(new OutboxMessage(eventType, payload));
        }

        // 3. Limpiar eventos del agregado para evitar doble procesamiento
        domainEntities.ForEach(e => e.Entity.ClearDomainEvents());
    }

    /// <summary>
    /// Maneja eventos que requieren crear entidades adicionales en la misma transacción.
    /// Se usa <c>context.Set&lt;T&gt;()</c> en lugar de la propiedad DbSet tipada para evitar
    /// acoplar el interceptor al <see cref="AppDbContext"/> concreto.
    /// </summary>
    private static void HandleSpecialEvents(DbContext context, Domain.Events.IDomainEvent domainEvent)
    {
        if (domainEvent is InventoryMovementRegisteredEvent inv)
        {
            var movement = new InventoryMovement(
                productId: inv.ProductId,
                quantity:  inv.Quantity,
                type:      inv.Type,
                referenceId: inv.ReferenceId,
                remarks:   inv.Remarks);

            movement.SetId(inv.MovementId);
            context.Set<InventoryMovement>().Add(movement);
        }
    }
}
