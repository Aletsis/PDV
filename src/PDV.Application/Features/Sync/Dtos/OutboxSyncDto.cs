namespace PDV.Application.Features.Sync.Dtos;

public record OutboxSyncDto(Guid MessageId, string EventType, string Payload, DateTime CreatedAt);
public record BatchSyncResult(List<MessageSyncResult> Results);
public record MessageSyncResult(Guid MessageId, bool Success, string? ErrorMessage);
