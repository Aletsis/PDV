using Microsoft.EntityFrameworkCore.ChangeTracking;
using PDV.Domain.Entities;
using System;
using System.Collections.Generic;

namespace PDV.Infrastructure.Persistence;

public class AuditEntry
{
    public AuditEntry(EntityEntry entry)
    {
        Entry = entry;
    }

    public EntityEntry Entry { get; }
    public string UserId { get; set; } = "System";
    public string TableName { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string? IpAddress { get; set; }
    public Dictionary<string, object?> KeyValues { get; } = new();
    public Dictionary<string, object?> OldValues { get; } = new();
    public Dictionary<string, object?> NewValues { get; } = new();

    public AuditLog ToAuditLog(DateTime timestamp, string? currentAction)
    {
        // Si hay una acción actual de MediatR, la usamos como ActionName. Si no, construimos una por defecto.
        var actionName = !string.IsNullOrEmpty(currentAction) 
            ? currentAction 
            : $"{Action} {TableName}";

        return new AuditLog(
            userId: UserId,
            actionName: actionName,
            timestamp: timestamp,
            oldValues: OldValues.Count == 0 ? null : System.Text.Json.JsonSerializer.Serialize(OldValues),
            newValues: NewValues.Count == 0 ? null : System.Text.Json.JsonSerializer.Serialize(NewValues),
            ipAddress: IpAddress
        );
    }
}
