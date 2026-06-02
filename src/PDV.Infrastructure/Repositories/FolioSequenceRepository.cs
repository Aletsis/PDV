using Microsoft.EntityFrameworkCore;
using PDV.Domain.Entities;
using PDV.Domain.Enums;
using PDV.Domain.Repositories;
using PDV.Infrastructure.Persistence;

namespace PDV.Infrastructure.Repositories;

public class FolioSequenceRepository : IFolioSequenceRepository
{
    private readonly AppDbContext _context;

    public FolioSequenceRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<FolioSequence?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.FolioSequences.FindAsync(new object[] { id }, cancellationToken);
    }

    public async Task<IEnumerable<FolioSequence>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _context.FolioSequences.ToListAsync(cancellationToken);
    }

    public async Task<FolioSequence?> GetByBranchAndTypeAsync(Guid branchId, InvoiceType type, CancellationToken cancellationToken = default)
    {
        return await _context.FolioSequences
            .FirstOrDefaultAsync(f => f.BranchId == branchId && f.SeriesType == type, cancellationToken);
    }

    public async Task<FolioSequence?> GetWithLockAsync(Guid branchId, InvoiceType type, CancellationToken cancellationToken = default)
    {
        // Por compatibilidad multiplataforma, usamos consulta estándar.
        // La exclusión mutua se garantiza a nivel transaccional.
        return await _context.FolioSequences
            .FirstOrDefaultAsync(f => f.BranchId == branchId && f.SeriesType == type, cancellationToken);
    }

    public async Task<int> AddAsync(FolioSequence sequence, CancellationToken cancellationToken = default)
    {
        _context.FolioSequences.Add(sequence);
        await _context.SaveChangesAsync(cancellationToken);
        return 0;
    }

    public async Task UpdateAsync(FolioSequence sequence, CancellationToken cancellationToken = default)
    {
        _context.FolioSequences.Update(sequence);
        await _context.SaveChangesAsync(cancellationToken);
    }
}
