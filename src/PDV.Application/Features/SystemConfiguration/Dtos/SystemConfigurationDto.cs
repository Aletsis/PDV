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
    
    // Ticket
    public int TicketWidth { get; set; } = 48; 
    public bool PrintLogoOnTicket { get; set; } = true;
    public bool AutoPrintTicket { get; set; } = true;
    public string? TicketHeader { get; set; }
    public string? TicketFooter { get; set; }
    
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

    // Integración API Comercial
    public string? ComercialApiUrl { get; set; }
    public string? ComercialApiKey { get; set; }
}
