using MediatR;
using Microsoft.EntityFrameworkCore;
using PDV.Application.Common.Interfaces;
using PDV.Application.Features.SystemConfiguration.Dtos;
using PDV.Domain.Repositories;

namespace PDV.Application.Features.SystemConfiguration.Queries.GetSystemConfiguration;

public record GetSystemConfigurationQuery : IRequest<SystemConfigurationDto>;

public class GetSystemConfigurationQueryHandler : IRequestHandler<GetSystemConfigurationQuery, SystemConfigurationDto>
{
    private readonly ISystemConfigurationRepository _repository;
    private readonly IApplicationDbContext _context;

    public GetSystemConfigurationQueryHandler(ISystemConfigurationRepository repository, IApplicationDbContext context)
    {
        _repository = repository;
        _context = context;
    }

    public async Task<SystemConfigurationDto> Handle(GetSystemConfigurationQuery request, CancellationToken cancellationToken)
    {
        var config = await _repository.GetAsync(cancellationToken);
        
        // Fetch all global logos
        var globalLogos = await _context.Logos
            .AsNoTracking()
            .Where(l => l.BranchId == null && l.IsActive)
            .ToListAsync(cancellationToken);
            
        var ticketLogo = globalLogos.FirstOrDefault(l => l.Purpose == PDV.Domain.Enums.LogoPurpose.Ticket);
        var cfdiLogo = globalLogos.FirstOrDefault(l => l.Purpose == PDV.Domain.Enums.LogoPurpose.Cfdi);
        var appLogo = globalLogos.FirstOrDefault(l => l.Purpose == PDV.Domain.Enums.LogoPurpose.App);
        
        if (config == null)
        {
            // Default config if not exists
            return new SystemConfigurationDto
            {
                 CompanyName = "Mi Empresa",
                 TicketWidth = 48,
                 PrintLogoOnTicket = true,
                 AutoPrintTicket = true,
                 LogoImage = ticketLogo?.Data,
                 LogoCfdiImage = cfdiLogo?.Data,
                 LogoAppImage = appLogo?.Data
            };
        }

        string addressStr = string.Empty;
        if (config.FiscalAddress != null)
        {
            addressStr = $"{config.FiscalAddress.Street}, {config.FiscalAddress.City}, {config.FiscalAddress.State}, CP {config.FiscalAddress.ZipCode}";
        }

        return new SystemConfigurationDto
        {
            CompanyName = config.CompanyName,
            TaxId = config.TaxId,
            FiscalRegime = config.FiscalRegime ?? "601",
            Address = addressStr,
            Phone = config.Phone ?? string.Empty,
            Email = config.Email,
            Currency = config.Currency,
            
            // Ticket
            TicketWidth = config.TicketWidth,
            PrintLogoOnTicket = config.PrintLogoOnTicket,
            AutoPrintTicket = config.AutoPrintTicket,
            TicketHeader = config.TicketHeader,
            TicketFooter = config.TicketFooter,
            
            // Logos
            LogoImage = ticketLogo?.Data,
            LogoCfdiImage = cfdiLogo?.Data,
            LogoAppImage = appLogo?.Data,

            // SMTP
            SmtpServer = config.SmtpServer,
            SmtpPort = config.SmtpPort,
            SmtpUser = config.SmtpUser,
            SmtpPassword = config.SmtpPassword,

            // Alertas
            AlertCashLimit = config.AlertCashLimit,
            AlertLateOpening = config.AlertLateOpening,
            AlertSystemFailure = config.AlertSystemFailure,
            AlertLateOrder = config.AlertLateOrder,

            // Respaldos
            BackupDirectory = config.BackupDirectory,

            // Automatización
            AutoReportEnabled = config.AutoReportEnabled,
            AutoReportUsers = config.AutoReportUsers,
            AutoReportTime = config.AutoReportTime,
            AutoBackupEnabled = config.AutoBackupEnabled,
            AutoBackupTime = config.AutoBackupTime,

            // API Comercial
            ComercialApiUrl = config.ComercialApiUrl,
            ComercialApiKey = config.ComercialApiKey
        };
    }
}
