using PDV.Domain.Enums;

namespace PDV.Domain.Events;

public record LogoUploadedEvent(Guid LogoId, string FileName, string ContentType, int FileSizeBytes) : IDomainEvent;
public record LogoReplacedEvent(Guid LogoId, string NewFileName) : IDomainEvent;
public record LogoDeactivatedEvent(Guid LogoId) : IDomainEvent;
