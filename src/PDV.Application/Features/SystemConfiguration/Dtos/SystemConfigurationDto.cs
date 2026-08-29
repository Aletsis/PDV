namespace PDV.Application.Features.SystemConfiguration.Dtos;

public class SystemConfigurationDto
{
    public string CompanyName { get; set; } = string.Empty;
    public string TaxId { get; set; } = string.Empty;
    public string FiscalRegime { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string Currency { get; set; } = "MXN";
    

    
    // Logos
    public byte[]? LogoImage { get; set; } // Ticket Logo
    public byte[]? LogoCfdiImage { get; set; }
    public byte[]? LogoAppImage { get; set; }

    // SMTP / Correo
    public string? SmtpServer { get; set; }
    public int? SmtpPort { get; set; }
    public string? SmtpUser { get; set; }
    public string? SmtpPassword { get; set; }

    // Alertas
    public bool AlertCashLimit { get; set; }
    public bool AlertLateOpening { get; set; }
    public bool AlertSystemFailure { get; set; }
    public bool AlertLateOrder { get; set; }

    // Respaldos
    public string? BackupDirectory { get; set; }

    // Automatización
    public bool AutoReportEnabled { get; set; }
    public string? AutoReportUsers { get; set; }
    public TimeSpan? AutoReportTime { get; set; }
    public bool AutoBackupEnabled { get; set; }
    public TimeSpan? AutoBackupTime { get; set; }

    // Surtido y Pedidos
    public int DefaultMaxPickingOrdersPerPicker { get; set; } = 1;

    // Integración API Comercial
    public string? ComercialApiUrl { get; set; }
    public string? ComercialApiKey { get; set; }

    // Facturación / CSD / PAC
    public string? CsdSerialNumber { get; set; }
    public DateTime? CsdExpiresAt { get; set; }
    public string? PacUrl { get; set; }
    public string? PacApiUser { get; set; }
    public string? PacApiKey { get; set; }
    public string? CsdPassword { get; set; }
    public bool HasCsdCertificate { get; set; }
    public bool HasCsdPrivateKey { get; set; }
}
