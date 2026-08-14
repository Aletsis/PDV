namespace PDV.Domain.Events;

public record SystemConfigurationUpdatedEvent(Guid ConfigId, string CompanyName, string TaxId) : IDomainEvent;
public record TicketSettingsUpdatedEvent(Guid ConfigId, int TicketCopies) : IDomainEvent;
public record InvoiceSettingsUpdatedEvent(Guid ConfigId, string CsdCertificateThumbprint) : IDomainEvent;
