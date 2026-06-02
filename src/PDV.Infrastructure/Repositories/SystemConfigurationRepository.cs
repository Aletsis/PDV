using Microsoft.EntityFrameworkCore;
using PDV.Domain.Entities;
using PDV.Domain.Repositories;
using PDV.Infrastructure.Persistence;

namespace PDV.Infrastructure.Repositories;

public class SystemConfigurationRepository : ISystemConfigurationRepository
{
    private readonly AppDbContext _context;

    public SystemConfigurationRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<SystemConfiguration?> GetAsync(CancellationToken cancellationToken = default)
    {
        return await _context.SystemConfigurations.FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<SystemConfiguration?> GetWithFiscalAddressAsync(CancellationToken cancellationToken = default)
    {
        return await _context.SystemConfigurations.FirstOrDefaultAsync(cancellationToken);
    }

    public async Task AddAsync(SystemConfiguration config, CancellationToken cancellationToken = default)
    {
        _context.SystemConfigurations.Add(config);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(SystemConfiguration config, CancellationToken cancellationToken = default)
    {
        _context.SystemConfigurations.Update(config);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<string?> GetTaxIdAsync(CancellationToken cancellationToken = default)
    {
        return await _context.SystemConfigurations
            .Select(c => c.TaxId)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<bool> IsCsdConfiguredAndValidAsync(CancellationToken cancellationToken = default)
    {
        return await _context.SystemConfigurations
            .AnyAsync(c => !string.IsNullOrEmpty(c.CsdSerialNumber) && c.CsdExpiresAt != null && c.CsdExpiresAt > DateTime.UtcNow, cancellationToken);
    }
}
