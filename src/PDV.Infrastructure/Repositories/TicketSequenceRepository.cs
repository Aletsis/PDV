using Microsoft.EntityFrameworkCore;
using PDV.Domain.Entities;
using PDV.Domain.Enums;
using PDV.Domain.Repositories;
using PDV.Infrastructure.Persistence;

namespace PDV.Infrastructure.Repositories;

public class TicketSequenceRepository : ITicketSequenceRepository
{
    private readonly AppDbContext _context;

    public TicketSequenceRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<TicketSequence?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.TicketSequences.FindAsync(new object[] { id }, cancellationToken);
    }

    public async Task<IEnumerable<TicketSequence>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _context.TicketSequences.ToListAsync(cancellationToken);
    }

    public async Task<TicketSequence?> GetByRegisterAndTypeAsync(Guid cashRegisterId, TicketSequenceType type, CancellationToken cancellationToken = default)
    {
        return await _context.TicketSequences
            .FirstOrDefaultAsync(t => t.CashRegisterId == cashRegisterId && t.SequenceType == type, cancellationToken);
    }

    public async Task<TicketSequence?> GetWithLockAsync(Guid cashRegisterId, TicketSequenceType type, CancellationToken cancellationToken = default)
    {
        // Por compatibilidad multiplataforma, usamos una consulta tradicional.
        // El bloqueo se controla a nivel de transacción de base de datos o Unit of Work.
        return await _context.TicketSequences
            .FirstOrDefaultAsync(t => t.CashRegisterId == cashRegisterId && t.SequenceType == type, cancellationToken);
    }

    public async Task<int> AddAsync(TicketSequence sequence, CancellationToken cancellationToken = default)
    {
        _context.TicketSequences.Add(sequence);
        await _context.SaveChangesAsync(cancellationToken);
        return 0;
    }

    public async Task UpdateAsync(TicketSequence sequence, CancellationToken cancellationToken = default)
    {
        _context.TicketSequences.Update(sequence);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<List<TicketSequence>> GetByCashRegisterAsync(Guid cashRegisterId, CancellationToken cancellationToken = default)
    {
        return await _context.TicketSequences
            .Where(t => t.CashRegisterId == cashRegisterId)
            .ToListAsync(cancellationToken);
    }
}
