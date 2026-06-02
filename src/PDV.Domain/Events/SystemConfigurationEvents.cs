namespace PDV.Domain.Events;

public record SystemConfigurationUpdatedEvent(Guid ConfigId, string CompanyName, string TaxId) : IDomainEvent;
public record TicketSettingsUpdatedEvent(Guid ConfigId, int TicketWidth, bool AutoPrint) : IDomainEvent;
public record InvoiceSettingsUpdatedEvent(Guid ConfigId, string CsdCertificateThumbprint) : IDomainEvent;
