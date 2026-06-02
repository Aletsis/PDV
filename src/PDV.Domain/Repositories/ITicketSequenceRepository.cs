using PDV.Domain.Entities;
using PDV.Domain.Enums;

namespace PDV.Domain.Repositories;

public interface ITicketSequenceRepository : IReadOnlyRepository<TicketSequence>
{
    /// <summary>
    /// Obtiene la secuencia de tickets para una caja y tipo de documento.
    /// </summary>
    Task<TicketSequence?> GetByRegisterAndTypeAsync(Guid cashRegisterId, TicketSequenceType type, CancellationToken cancellationToken = default);

    /// <summary>
    /// Obtiene la secuencia de tickets adquiriendo un bloqueo pesimista (UPDLOCK)
    /// para garantizar que nadie más pueda leerla hasta que termine la transacción.
    /// </summary>
    Task<TicketSequence?> GetWithLockAsync(Guid cashRegisterId, TicketSequenceType type, CancellationToken cancellationToken = default);

    Task UpdateAsync(TicketSequence sequence, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Obtiene todas las secuencias de una caja (útil para reiniciarlas por turno).
    /// </summary>
    Task<List<TicketSequence>> GetByCashRegisterAsync(Guid cashRegisterId, CancellationToken cancellationToken = default);
}
