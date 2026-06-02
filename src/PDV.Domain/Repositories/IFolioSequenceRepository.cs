using PDV.Domain.Entities;
using PDV.Domain.Enums;

namespace PDV.Domain.Repositories;

public interface IFolioSequenceRepository : IReadOnlyRepository<FolioSequence>
{
    /// <summary>
    /// Obtiene la secuencia de folios para una sucursal y tipo de documento.
    /// </summary>
    Task<FolioSequence?> GetByBranchAndTypeAsync(Guid branchId, InvoiceType type, CancellationToken cancellationToken = default);

    /// <summary>
    /// Obtiene la secuencia de folios adquiriendo un bloqueo pesimista (UPDLOCK)
    /// para garantizar que nadie más pueda leerla hasta que termine la transacción.
    /// </summary>
    Task<FolioSequence?> GetWithLockAsync(Guid branchId, InvoiceType type, CancellationToken cancellationToken = default);

    Task UpdateAsync(FolioSequence sequence, CancellationToken cancellationToken = default);
}
