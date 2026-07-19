using System;

namespace PDV.Domain.Entities;

public class AuditLog
{
    public Guid Id { get; private set; }
    public string UserId { get; private set; } = string.Empty;
    public string ActionName { get; private set; } = string.Empty;
    public DateTime Timestamp { get; private set; }
    public string? OldValues { get; private set; } // Representación JSON de valores originales
    public string? NewValues { get; private set; } // Representación JSON de valores nuevos
    public string? IpAddress { get; private set; }

    private AuditLog() { }

    public AuditLog(string userId, string actionName, DateTime timestamp, string? oldValues, string? newValues, string? ipAddress)
    {
        Id = Guid.NewGuid();
        UserId = userId ?? "System";
        ActionName = actionName ?? "Database Change";
        Timestamp = timestamp;
        OldValues = oldValues;
        NewValues = newValues;
        IpAddress = ipAddress;
    }
}
