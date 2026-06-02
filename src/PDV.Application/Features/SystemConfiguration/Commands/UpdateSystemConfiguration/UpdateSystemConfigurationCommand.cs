using MediatR;
using Microsoft.EntityFrameworkCore;
using PDV.Application.Common.Interfaces;
using PDV.Domain.Entities;
using PDV.Domain.Repositories;
using PDV.Domain.ValueObjects;
using PDV.Domain.Enums;

namespace PDV.Application.Features.SystemConfiguration.Commands.UpdateSystemConfiguration;

public record UpdateSystemConfigurationCommand(
    string CompanyName,
    string TaxId,
    string FiscalRegime,
    string Address,
    string Phone,
    string? Email,
    
    // Ticket
    int TicketWidth,
    bool PrintLogoOnTicket,
    bool AutoPrintTicket,
    string? TicketHeader,
    string? TicketFooter,

    // SMTP / Correo
    string? SmtpServer,
    int? SmtpPort,
    string? SmtpUser,
    string? SmtpPassword,

    // Alertas
    bool AlertCashLimit,
    bool AlertLateOpening,
    bool AlertSystemFailure,
    bool AlertLateOrder,

    // Respaldos
    string? BackupDirectory,

    // Automatización
    bool AutoReportEnabled,
    string? AutoReportUsers,
    TimeSpan? AutoReportTime,
    bool AutoBackupEnabled,
    TimeSpan? AutoBackupTime,

    // Logos
    byte[]? LogoImage = null,
    byte[]? LogoCfdiImage = null,
    byte[]? LogoAppImage = null,

    // API Comercial
    string? ComercialApiUrl = null,
    string? ComercialApiKey = null
) : IRequest;

public class UpdateSystemConfigurationCommandHandler : IRequestHandler<UpdateSystemConfigurationCommand>
{
    private readonly ISystemConfigurationRepository _repository;
    private readonly IApplicationDbContext _context;

    public UpdateSystemConfigurationCommandHandler(ISystemConfigurationRepository repository, IApplicationDbContext context)
    {
        _repository = repository;
        _context = context;
    }

    public async Task Handle(UpdateSystemConfigurationCommand request, CancellationToken cancellationToken)
    {
        var config = await _repository.GetAsync(cancellationToken);

        if (config == null)
        {
            config = new PDV.Domain.Entities.SystemConfiguration(
                request.CompanyName,
                request.TaxId,
                request.FiscalRegime,
                "MXN",
                request.Phone,
                request.Email
            );
            
            if (!string.IsNullOrWhiteSpace(request.Address))
            {
                config.SetFiscalAddress(Address.Create(request.Address, "N/A", "N/A", "00000", "México"));
            }

            config.UpdateTicketSettings(request.TicketWidth, request.PrintLogoOnTicket, request.AutoPrintTicket, 1, request.TicketHeader, request.TicketFooter);
            config.UpdateSmtpSettings(request.SmtpServer, request.SmtpPort, request.SmtpUser, request.SmtpPassword);
            config.UpdateAlertSettings(request.AlertCashLimit, request.AlertLateOpening, request.AlertSystemFailure, request.AlertLateOrder);
            config.UpdateBackupSettings(request.BackupDirectory);
            config.UpdateAutomationSettings(request.AutoReportEnabled, request.AutoReportUsers, request.AutoReportTime, request.AutoBackupEnabled, request.AutoBackupTime);
            config.UpdateComercialApiSettings(request.ComercialApiUrl, request.ComercialApiKey);
            
            await _repository.AddAsync(config, cancellationToken);
        }
        else
        {
            config.UpdateCompanyInfo(
                request.CompanyName,
                request.TaxId,
                request.FiscalRegime,
                request.Phone,
                request.Email);
            
            if (!string.IsNullOrWhiteSpace(request.Address))
            {
                config.SetFiscalAddress(Address.Create(request.Address, "N/A", "N/A", "00000", "México"));
            }
            
            config.UpdateTicketSettings(request.TicketWidth, request.PrintLogoOnTicket, request.AutoPrintTicket, 1, request.TicketHeader, request.TicketFooter);
            config.UpdateSmtpSettings(request.SmtpServer, request.SmtpPort, request.SmtpUser, request.SmtpPassword);
            config.UpdateAlertSettings(request.AlertCashLimit, request.AlertLateOpening, request.AlertSystemFailure, request.AlertLateOrder);
            config.UpdateBackupSettings(request.BackupDirectory);
            config.UpdateAutomationSettings(request.AutoReportEnabled, request.AutoReportUsers, request.AutoReportTime, request.AutoBackupEnabled, request.AutoBackupTime);
            config.UpdateComercialApiSettings(request.ComercialApiUrl, request.ComercialApiKey);
            
            await _repository.UpdateAsync(config, cancellationToken);
        }

        // Save individual logos
        await SaveOrUpdateLogo(request.LogoImage, LogoPurpose.Ticket, cancellationToken);
        await SaveOrUpdateLogo(request.LogoCfdiImage, LogoPurpose.Cfdi, cancellationToken);
        await SaveOrUpdateLogo(request.LogoAppImage, LogoPurpose.App, cancellationToken);
        
        await _context.SaveChangesAsync(cancellationToken);
    }

    private async Task SaveOrUpdateLogo(byte[]? data, LogoPurpose purpose, CancellationToken cancellationToken)
    {
        if (data == null || data.Length == 0) return;

        var existingLogo = await _context.Logos
            .FirstOrDefaultAsync(l => l.Purpose == purpose && l.BranchId == null, cancellationToken);

        if (existingLogo != null)
        {
            existingLogo.Replace($"logo_{purpose.ToString().ToLower()}.png", "image/png", data);
        }
        else
        {
            var logo = new Logo($"logo_{purpose.ToString().ToLower()}.png", "image/png", data, purpose, null);
            _context.Logos.Add(logo);
        }
    }
}
