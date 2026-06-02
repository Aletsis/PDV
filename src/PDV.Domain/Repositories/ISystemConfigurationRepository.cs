using PDV.Domain.Entities;

namespace PDV.Domain.Repositories;

public interface ISystemConfigurationRepository
{
    /// <summary>Obtiene la configuración global del sistema.</summary>
    Task<SystemConfiguration?> GetAsync(CancellationToken cancellationToken = default);

    /// <summary>Obtiene la configuración global incluyendo la dirección fiscal cargada.</summary>
    Task<SystemConfiguration?> GetWithFiscalAddressAsync(CancellationToken cancellationToken = default);

    /// <summary>Guarda la configuración por primera vez.</summary>
    Task AddAsync(SystemConfiguration config, CancellationToken cancellationToken = default);

    /// <summary>Actualiza la configuración existente.</summary>
    Task UpdateAsync(SystemConfiguration config, CancellationToken cancellationToken = default);

    /// <summary>Obtiene rápidamente el RFC de la empresa configurada sin cargar todo el objeto.</summary>
    Task<string?> GetTaxIdAsync(CancellationToken cancellationToken = default);

    /// <summary>Verifica de forma rápida si el CSD (Certificado de Sello Digital) está configurado y vigente.</summary>
    Task<bool> IsCsdConfiguredAndValidAsync(CancellationToken cancellationToken = default);
}
