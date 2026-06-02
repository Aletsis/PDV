using Microsoft.EntityFrameworkCore;
using PDV.Domain.Entities;
using PDV.Domain.Repositories;
using PDV.Infrastructure.Persistence;

namespace PDV.Infrastructure.Repositories;

public class BranchRepository : IBranchRepository
{
    private readonly AppDbContext _context;

    public BranchRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<Branch?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Branches.FindAsync(new object[] { id }, cancellationToken);
    }

    public async Task<Branch?> GetByCodeAsync(string code, CancellationToken cancellationToken = default)
    {
        return await _context.Branches
            .FirstOrDefaultAsync(b => b.Code == code, cancellationToken);
    }

    public async Task<IEnumerable<Branch>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Branches.OrderBy(b => b.Name).ToListAsync(cancellationToken);
    }

    public async Task<List<Branch>> GetAllActiveAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Branches
            .Where(b => b.IsActive)
            .OrderBy(b => b.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<Branch>> GetAllInactiveAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Branches
            .Where(b => !b.IsActive)
            .OrderBy(b => b.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<Branch?> GetMainBranchAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Branches
            .FirstOrDefaultAsync(b => b.IsMainBranch, cancellationToken);
    }

    public async Task<int> AddAsync(Branch branch, CancellationToken cancellationToken = default)
    {
        _context.Branches.Add(branch);
        await _context.SaveChangesAsync(cancellationToken);
        return 0;
    }

    public async Task UpdateAsync(Branch branch, CancellationToken cancellationToken = default)
    {
        _context.Branches.Update(branch);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(Branch branch, CancellationToken cancellationToken = default)
    {
        _context.Branches.Remove(branch);
        await _context.SaveChangesAsync(cancellationToken);
    }
}
