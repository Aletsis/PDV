using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PDV.Application.Common.Interfaces;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace PDV.Application.Features.SystemConfiguration.Commands.UpdateInvoiceSettings;

public record UpdateInvoiceSettingsCommand(
    string PacUrl,
    string PacApiUser,
    string? PacApiKey,
    byte[]? CsdCertificateData,
    byte[]? CsdPrivateKeyData,
    string? CsdPassword
) : IRequest<bool>;

public class UpdateInvoiceSettingsCommandValidator : AbstractValidator<UpdateInvoiceSettingsCommand>
{
    public UpdateInvoiceSettingsCommandValidator()
    {
        RuleFor(v => v.PacUrl)
            .NotEmpty().WithMessage("La URL del PAC es requerida");

        RuleFor(v => v.PacApiUser)
            .NotEmpty().WithMessage("El usuario de API del PAC es requerido");
    }
}

public class UpdateInvoiceSettingsCommandHandler : IRequestHandler<UpdateInvoiceSettingsCommand, bool>
{
    private readonly IApplicationDbContext _context;
    private readonly ICsdCertificateService _csdCertificateService;

    public UpdateInvoiceSettingsCommandHandler(
        IApplicationDbContext context,
        ICsdCertificateService csdCertificateService)
    {
        _context = context;
        _csdCertificateService = csdCertificateService;
    }

    public async Task<bool> Handle(UpdateInvoiceSettingsCommand request, CancellationToken cancellationToken)
    {
        var config = await _context.SystemConfigurations.FirstOrDefaultAsync(cancellationToken);
        if (config == null)
        {
            throw new InvalidOperationException("La configuración del sistema no ha sido inicializada. Inicialice la información general de la empresa primero.");
        }

        string serialNumber = config.CsdSerialNumber ?? string.Empty;
        DateTime expiresAt = config.CsdExpiresAt ?? DateTime.Now.AddYears(1);

        if (request.CsdCertificateData != null && request.CsdCertificateData.Length > 0)
        {
            try
            {
                var metadata = _csdCertificateService.ExtractMetadata(request.CsdCertificateData);
                serialNumber = metadata.SerialNumber;
                expiresAt = metadata.ExpiresAt;

                if (!string.Equals(metadata.RfcEmisor, config.TaxId, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException($"El RFC del certificado ({metadata.RfcEmisor}) no coincide con el RFC configurado para la empresa ({config.TaxId}).");
                }
            }
            catch (Exception ex) when (ex is not InvalidOperationException)
            {
                throw new InvalidOperationException($"El archivo .cer no es un certificado X.509 de SAT válido: {ex.Message}");
            }
        }

        config.UpdateInvoiceSettings(
            csdSerialNumber: serialNumber,
            csdExpiresAt: expiresAt,
            pacUrl: request.PacUrl,
            pacApiUser: request.PacApiUser,
            pacApiKey: request.PacApiKey,
            csdCertificateData: request.CsdCertificateData,
            csdPrivateKeyData: request.CsdPrivateKeyData,
            csdPassword: request.CsdPassword
        );

        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }
}
