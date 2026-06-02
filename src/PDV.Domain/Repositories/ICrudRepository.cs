namespace PDV.Domain.Repositories;

public interface ICrudRepository<T> : IReadOnlyRepository<T> where T : class
{
    /// <summary>Actualiza una entidad existente.</summary>
    Task UpdateAsync(T entity, CancellationToken cancellationToken = default);
    
    /// <summary>Elimina una entidad.</summary>
    Task DeleteAsync(T entity, CancellationToken cancellationToken = default);
}
