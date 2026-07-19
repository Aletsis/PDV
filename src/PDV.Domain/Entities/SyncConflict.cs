using System;
using PDV.Domain.Common;

namespace PDV.Domain.Entities;

public class SyncConflict : BaseEntity
{
    public string EntityName { get; private set; } = string.Empty;
    public Guid EntityId { get; private set; }
    public string ClientValuesJson { get; private set; } = string.Empty;
    public string ServerValuesJson { get; private set; } = string.Empty;
    public string ConflictType { get; private set; } = string.Empty;
    public DateTime DetectedAt { get; private set; }
    public bool Resolved { get; private set; }
    public DateTime? ResolvedAt { get; private set; }
    public string? ResolutionStrategy { get; private set; }

#pragma warning disable CS8618
    private SyncConflict() { } // EF Core
#pragma warning restore CS8618

    public SyncConflict(string entityName, Guid entityId, string clientValuesJson, string serverValuesJson, string conflictType)
    {
        EntityName = entityName;
        EntityId = entityId;
        ClientValuesJson = clientValuesJson;
        ServerValuesJson = serverValuesJson;
        ConflictType = conflictType;
        DetectedAt = DateTime.UtcNow;
        Resolved = false;
    }

    public void Resolve(string strategy)
    {
        Resolved = true;
        ResolvedAt = DateTime.UtcNow;
        ResolutionStrategy = strategy;
    }
}
