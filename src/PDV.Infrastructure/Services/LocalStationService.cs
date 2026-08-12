using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PDV.Application.Common.Interfaces;
using PDV.Application.Features.CashRegisters.Dtos;
using PDV.Domain.Enums;

namespace PDV.Infrastructure.Services;

public class LocalStationService : ILocalStationService
{
    private static readonly string ConfigFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "local_station.json");
    private readonly IApplicationDbContext _context;
    private readonly ILogger<LocalStationService> _logger;

    public LocalStationService(IApplicationDbContext context, ILogger<LocalStationService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public class StationConfigFile
    {
        public Guid? CashRegisterId { get; set; }
        public string? CashRegisterName { get; set; }
        public DateTime? BoundAt { get; set; }
    }

    public async Task<Guid?> GetAssignedCashRegisterIdAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            if (File.Exists(ConfigFilePath))
            {
                var json = await File.ReadAllTextAsync(ConfigFilePath, cancellationToken);
                var config = JsonSerializer.Deserialize<StationConfigFile>(json);
                if (config?.CashRegisterId.HasValue == true && config.CashRegisterId.Value != Guid.Empty)
                {
                    // Validar que la caja aún exista en la base de datos local
                    var exists = await _context.CashRegisters
                        .AnyAsync(c => c.Id == config.CashRegisterId.Value, cancellationToken);
                    if (exists)
                    {
                        return config.CashRegisterId.Value;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error reading local station config file from {Path}", ConfigFilePath);
        }

        return null;
    }

    public async Task SetAssignedCashRegisterIdAsync(Guid cashRegisterId, CancellationToken cancellationToken = default)
    {
        try
        {
            var register = await _context.CashRegisters
                .FirstOrDefaultAsync(c => c.Id == cashRegisterId, cancellationToken);

            var config = new StationConfigFile
            {
                CashRegisterId = cashRegisterId,
                CashRegisterName = register?.Name ?? "Desconocida",
                BoundAt = DateTime.UtcNow
            };

            var json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(ConfigFilePath, json, cancellationToken);
            _logger.LogInformation("Local station successfully bound to CashRegister {Id} ({Name})", cashRegisterId, register?.Name);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error writing local station config file to {Path}", ConfigFilePath);
            throw;
        }
    }

    public async Task<CashRegisterDto?> GetCurrentCashRegisterAsync(CancellationToken cancellationToken = default)
    {
        var assignedId = await GetAssignedCashRegisterIdAsync(cancellationToken);
        if (assignedId.HasValue)
        {
            var reg = await _context.CashRegisters
                .Include(c => c.Branch)
                .Include(c => c.AssignedPrinter)
                .FirstOrDefaultAsync(c => c.Id == assignedId.Value, cancellationToken);

            if (reg != null)
            {
                return new CashRegisterDto
                {
                    Id                  = reg.Id,
                    Name                = reg.Name,
                    Location            = reg.Location,
                    BranchId            = reg.BranchId,
                    BranchName          = reg.Branch?.Name ?? "Sin Sucursal",
                    AssignedUserId      = reg.AssignedUserId,
                    AssignedUserName    = null,
                    AssignedPrinterId   = reg.AssignedPrinterId,
                    AssignedPrinterName = reg.AssignedPrinter?.Name,
                    IsActive            = reg.IsActive,
                    IpAddress           = reg.IpAddress,
                    Mode                = reg.Mode
                };
            }
        }

        return null;
    }

    public Task ClearAssignedCashRegisterIdAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            if (File.Exists(ConfigFilePath))
            {
                File.Delete(ConfigFilePath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error deleting local station config file from {Path}", ConfigFilePath);
        }
        return Task.CompletedTask;
    }
}
