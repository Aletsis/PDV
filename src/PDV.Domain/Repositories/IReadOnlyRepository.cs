namespace PDV.Domain.Repositories;

public interface IReadOnlyRepository<T> where T : class
{
    /// <summary>Obtiene una entidad por su ID.</summary>
    Task<T?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    
    /// <summary>Obtiene todas las entidades.</summary>
    Task<IEnumerable<T>> GetAllAsync(CancellationToken cancellationToken = default);
    
    /// <summary>Agrega una nueva entidad.</summary>
    Task<int> AddAsync(T entity, CancellationToken cancellationToken = default);
}
