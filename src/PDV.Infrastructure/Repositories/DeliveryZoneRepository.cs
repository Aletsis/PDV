using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using PDV.Domain.Entities;
using PDV.Domain.Repositories;
using PDV.Infrastructure.Persistence;

namespace PDV.Infrastructure.Repositories;

public class DeliveryZoneRepository : IDeliveryZoneRepository
{
    private readonly AppDbContext _context;

    public DeliveryZoneRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<DeliveryZone?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.DeliveryZones.FindAsync(new object[] { id }, cancellationToken);
    }

    public async Task<IEnumerable<DeliveryZone>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _context.DeliveryZones.ToListAsync(cancellationToken);
    }

    public Task<int> AddAsync(DeliveryZone entity, CancellationToken cancellationToken = default)
    {
        _context.DeliveryZones.Add(entity);
        return Task.FromResult(0);
    }

    public Task UpdateAsync(DeliveryZone entity, CancellationToken cancellationToken = default)
    {
        _context.DeliveryZones.Update(entity);
        return Task.CompletedTask;
    }

    public async Task DeleteAsync(DeliveryZone entity, CancellationToken cancellationToken = default)
    {
        entity.SoftDelete("system");
        _context.DeliveryZones.Update(entity);
        await Task.CompletedTask;
    }

    public async Task<List<DeliveryZone>> GetByBranchIdAsync(Guid branchId, CancellationToken cancellationToken = default)
    {
        return await _context.DeliveryZones
            .Where(z => z.BranchId == branchId)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<DeliveryZone>> GetActiveZonesByBranchIdAsync(Guid branchId, CancellationToken cancellationToken = default)
    {
        return await _context.DeliveryZones
            .Where(z => z.BranchId == branchId && z.IsActive)
            .ToListAsync(cancellationToken);
    }
}
