using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using PDV.Domain.Entities;
using PDV.Domain.Enums;
using PDV.Domain.Repositories;
using PDV.Infrastructure.Persistence;

namespace PDV.Infrastructure.Repositories;

public class DeliveryRouteRepository : IDeliveryRouteRepository
{
    private readonly AppDbContext _context;

    public DeliveryRouteRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<DeliveryRoute?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.DeliveryRoutes.FindAsync(new object[] { id }, cancellationToken);
    }

    public async Task<IEnumerable<DeliveryRoute>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _context.DeliveryRoutes.ToListAsync(cancellationToken);
    }

    public Task<int> AddAsync(DeliveryRoute entity, CancellationToken cancellationToken = default)
    {
        _context.DeliveryRoutes.Add(entity);
        return Task.FromResult(0);
    }

    public Task UpdateAsync(DeliveryRoute entity, CancellationToken cancellationToken = default)
    {
        _context.DeliveryRoutes.Update(entity);
        return Task.CompletedTask;
    }

    public async Task DeleteAsync(DeliveryRoute entity, CancellationToken cancellationToken = default)
    {
        entity.SoftDelete("system");
        _context.DeliveryRoutes.Update(entity);
        await Task.CompletedTask;
    }

    public async Task<DeliveryRoute?> GetByIdWithOrdersAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.DeliveryRoutes
            .Include(r => r.Orders)
            .ThenInclude(o => o.Items)
            .Include(r => r.Orders)
            .ThenInclude(o => o.Client)
            .Include(r => r.DeliveryZone)
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
    }

    public async Task<List<DeliveryRoute>> GetByDeliveryManIdAsync(string deliveryManId, CancellationToken cancellationToken = default)
    {
        return await _context.DeliveryRoutes
            .Where(r => r.DeliveryManId == deliveryManId)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<DeliveryRoute>> GetByBranchIdAsync(Guid branchId, CancellationToken cancellationToken = default)
    {
        return await _context.DeliveryRoutes
            .Where(r => r.BranchId == branchId)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<DeliveryRoute>> GetByStatusAsync(DeliveryRouteStatus status, CancellationToken cancellationToken = default)
    {
        return await _context.DeliveryRoutes
            .Where(r => r.Status == status)
            .ToListAsync(cancellationToken);
    }

    public async Task<int> GetNextFolioAsync(Guid branchId, CancellationToken cancellationToken = default)
    {
        var maxFolio = await _context.DeliveryRoutes
            .IgnoreQueryFilters() // Considerar rutas eliminadas para evitar colisiones
            .Where(r => r.BranchId == branchId)
            .Select(r => (int?)r.Folio)
            .MaxAsync(cancellationToken);

        return (maxFolio ?? 0) + 1;
    }
}
