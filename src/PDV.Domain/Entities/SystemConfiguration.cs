using PDV.Domain.Common;
using PDV.Domain.Events;
using PDV.Domain.Exceptions;
using PDV.Domain.ValueObjects;

namespace PDV.Domain.Entities;

/// <summary>
/// Agregado raíz de configuración global del sistema.
/// Singleton: solo existe un registro por instancia del POS.
/// Contiene datos fiscales (emisor CFDI), configuración de tickets y conexión con el PAC.
/// </summary>
public class SystemConfiguration : BaseEntity, IAggregateRoot
{
    // ──────────────────────────────────────────────
    // Datos del Emisor (Empresa)
    // ──────────────────────────────────────────────
    public string CompanyName { get; private set; }
    /// <summary>RFC del emisor de CFDI. Debe tener formato válido.</summary>
    public string TaxId { get; private set; }
    public string? Email { get; private set; }
    public string? Phone { get; private set; }
    public string Currency { get; private set; }

    public Address? FiscalAddress { get; private set; }

    // ──────────────────────────────────────────────
    // Configuración Fiscal / CFDI
    // Datos del CSD (Certificado de Sello Digital) y conexión PAC
    // ──────────────────────────────────────────────
    /// <summary>Número de serie del CSD cargado en el sistema.</summary>
    public string? CsdSerialNumber { get; private set; }
    /// <summary>Fecha de vencimiento del CSD.</summary>
    public DateTime? CsdExpiresAt { get; private set; }
    /// <summary>URL del PAC (Proveedor Autorizado de Certificación) para timbrado.</summary>
    public string? PacUrl { get; private set; }
    /// <summary>Usuario/llave de API del PAC.</summary>
    public string? PacApiUser { get; private set; }
    /// <summary>Régimen fiscal del emisor (catálogo SAT c_RegimenFiscal). Ej: "601" = General de Ley.</summary>
    public string FiscalRegime { get; private set; }

    // ──────────────────────────────────────────────
    // Configuración de Tickets
    // ──────────────────────────────────────────────
    /// <summary>Ancho del ticket en caracteres (32–80). 48 = papel 80mm estándar.</summary>
    public int TicketWidth { get; private set; }
    public bool PrintLogoOnTicket { get; private set; }
    public bool AutoPrintTicket { get; private set; }
    /// <summary>Número de copias a imprimir por ticket.</summary>
    public int TicketCopies { get; private set; }
    /// <summary>Texto o leyenda al pie del ticket (ej. "Gracias por su compra").</summary>
    public string? TicketFooter { get; private set; }
    /// <summary>Texto de encabezado del ticket.</summary>
    public string? TicketHeader { get; private set; }

    // ──────────────────────────────────────────────
    // Configuración de Correo / SMTP
    // ──────────────────────────────────────────────
    public string? SmtpServer { get; private set; }
    public int? SmtpPort { get; private set; }
    public string? SmtpUser { get; private set; }
    public string? SmtpPassword { get; private set; }

    // ──────────────────────────────────────────────
    // Configuración de Alertas
    // ──────────────────────────────────────────────
    public bool AlertCashLimit { get; private set; }
    public bool AlertLateOpening { get; private set; }
    public bool AlertSystemFailure { get; private set; }
    public bool AlertLateOrder { get; private set; }

    // ──────────────────────────────────────────────
    // Configuración de Respaldos
    // ──────────────────────────────────────────────
    public string? BackupDirectory { get; private set; }

    // ──────────────────────────────────────────────
    // Configuración de Automatización
    // ──────────────────────────────────────────────
    public bool AutoReportEnabled { get; private set; }
    public string? AutoReportUsers { get; private set; }
    public TimeSpan? AutoReportTime { get; private set; }
    public bool AutoBackupEnabled { get; private set; }
    public TimeSpan? AutoBackupTime { get; private set; }

    // ──────────────────────────────────────────────
    // Integración API Comercial
    // ──────────────────────────────────────────────
    /// <summary>URL base de la API del sistema Comercial (ej. https://api.comercial.com).</summary>
    public string? ComercialApiUrl { get; private set; }
    /// <summary>Llave de API para autenticación con el sistema Comercial.</summary>
    public string? ComercialApiKey { get; private set; }

#pragma warning disable CS8618
    private SystemConfiguration() { } // Para EF Core
#pragma warning restore CS8618

    public SystemConfiguration(
        string companyName,
        string taxId,
        string fiscalRegime,
        string currency = "MXN",
        string? phone = null,
        string? email = null)
    {
        ValidateTaxId(taxId);
        if (string.IsNullOrWhiteSpace(companyName)) throw new DomainException("El nombre de la empresa es requerido.");
        if (string.IsNullOrWhiteSpace(fiscalRegime)) throw new DomainException("El régimen fiscal es requerido.");

        CompanyName = companyName.Trim();
        TaxId = taxId.Trim().ToUpperInvariant();
        FiscalRegime = fiscalRegime.Trim();
        Currency = currency ?? "MXN";
        Phone = phone?.Trim();
        Email = email?.Trim();

        // Defaults para tickets
        TicketWidth = 48;
        PrintLogoOnTicket = true;
        AutoPrintTicket = true;
        TicketCopies = 1;

        AddDomainEvent(new SystemConfigurationUpdatedEvent(Id, CompanyName, TaxId));
    }

    // ──────────────────────────────────────────────
    // Actualización de Datos de la Empresa
    // ──────────────────────────────────────────────

    public void UpdateCompanyInfo(
        string companyName,
        string taxId,
        string fiscalRegime,
        string? phone = null,
        string? email = null)
    {
        ValidateTaxId(taxId);
        if (string.IsNullOrWhiteSpace(companyName)) throw new DomainException("El nombre de la empresa es requerido.");
        if (string.IsNullOrWhiteSpace(fiscalRegime)) throw new DomainException("El régimen fiscal es requerido.");

        CompanyName = companyName.Trim();
        TaxId = taxId.Trim().ToUpperInvariant();
        FiscalRegime = fiscalRegime.Trim();
        Phone = phone?.Trim();
        Email = email?.Trim();

        AddDomainEvent(new SystemConfigurationUpdatedEvent(Id, CompanyName, TaxId));
    }

    public void SetFiscalAddress(Address address)
    {
        FiscalAddress = address ?? throw new DomainException("La dirección fiscal no puede ser nula.");
    }

    // ──────────────────────────────────────────────
    // Configuración del CSD y PAC
    // ──────────────────────────────────────────────

    public void UpdateInvoiceSettings(
        string csdSerialNumber,
        DateTime csdExpiresAt,
        string pacUrl,
        string pacApiUser)
    {
        if (string.IsNullOrWhiteSpace(csdSerialNumber)) throw new DomainException("El número de serie del CSD es requerido.");
        if (csdExpiresAt <= DateTime.UtcNow) throw new DomainException("El CSD ya está vencido. Cargue un certificado vigente.");
        if (string.IsNullOrWhiteSpace(pacUrl)) throw new DomainException("La URL del PAC es requerida.");
        if (string.IsNullOrWhiteSpace(pacApiUser)) throw new DomainException("El usuario del PAC es requerido.");

        CsdSerialNumber = csdSerialNumber.Trim();
        CsdExpiresAt = csdExpiresAt;
        PacUrl = pacUrl.Trim();
        PacApiUser = pacApiUser.Trim();

        AddDomainEvent(new InvoiceSettingsUpdatedEvent(Id, CsdSerialNumber));
    }

    public bool IsCsdValid() =>
        !string.IsNullOrWhiteSpace(CsdSerialNumber) &&
        CsdExpiresAt.HasValue &&
        CsdExpiresAt.Value > DateTime.UtcNow;

    // ──────────────────────────────────────────────
    // Configuración de Tickets
    // ──────────────────────────────────────────────

    public void UpdateTicketSettings(
        int ticketWidth,
        bool printLogo,
        bool autoPrint,
        int ticketCopies = 1,
        string? header = null,
        string? footer = null)
    {
        if (ticketWidth < 32 || ticketWidth > 80)
            throw new DomainException("El ancho del ticket debe estar entre 32 y 80 caracteres.");
        if (ticketCopies < 1 || ticketCopies > 5)
            throw new DomainException("El número de copias debe estar entre 1 y 5.");

        TicketWidth = ticketWidth;
        PrintLogoOnTicket = printLogo;
        AutoPrintTicket = autoPrint;
        TicketCopies = ticketCopies;
        TicketHeader = header?.Trim();
        TicketFooter = footer?.Trim();

        AddDomainEvent(new TicketSettingsUpdatedEvent(Id, TicketWidth, AutoPrintTicket));
    }

    // ──────────────────────────────────────────────
    // Configuración de Correo / SMTP
    // ──────────────────────────────────────────────

    public void UpdateSmtpSettings(string? server, int? port, string? user, string? password)
    {
        SmtpServer = server?.Trim();
        SmtpPort = port;
        SmtpUser = user?.Trim();
        SmtpPassword = password; // No trim password
    }

    // ──────────────────────────────────────────────
    // Configuración de Alertas
    // ──────────────────────────────────────────────

    public void UpdateAlertSettings(bool cashLimit, bool lateOpening, bool systemFailure, bool lateOrder)
    {
        AlertCashLimit = cashLimit;
        AlertLateOpening = lateOpening;
        AlertSystemFailure = systemFailure;
        AlertLateOrder = lateOrder;
    }

    // ──────────────────────────────────────────────
    // Configuración de Respaldos
    // ──────────────────────────────────────────────

    public void UpdateBackupSettings(string? directory)
    {
        BackupDirectory = directory?.Trim();
    }

    // ──────────────────────────────────────────────
    // Configuración de Automatización
    // ──────────────────────────────────────────────

    public void UpdateAutomationSettings(
        bool autoReportEnabled,
        string? autoReportUsers,
        TimeSpan? autoReportTime,
        bool autoBackupEnabled,
        TimeSpan? autoBackupTime)
    {
        AutoReportEnabled = autoReportEnabled;
        AutoReportUsers = autoReportUsers?.Trim();
        AutoReportTime = autoReportTime;
        AutoBackupEnabled = autoBackupEnabled;
        AutoBackupTime = autoBackupTime;
    }

    // ──────────────────────────────────────────────
    // Integración API Comercial
    // ──────────────────────────────────────────────

    public void UpdateComercialApiSettings(string? apiUrl, string? apiKey)
    {
        ComercialApiUrl = apiUrl?.Trim();
        ComercialApiKey = apiKey; // No trim API keys
    }

    // ──────────────────────────────────────────────
    // Helpers privados
    // ──────────────────────────────────────────────

    private static void ValidateTaxId(string taxId)
    {
        if (string.IsNullOrWhiteSpace(taxId))
            throw new DomainException("El RFC (TaxId) es requerido.");

        // RFC persona física: 13 caracteres. Persona moral: 12 caracteres.
        var rfc = taxId.Trim().ToUpperInvariant();
        if (rfc.Length < 12 || rfc.Length > 13)
            throw new DomainException($"El RFC '{rfc}' no tiene una longitud válida (12 o 13 caracteres).");
    }
}
