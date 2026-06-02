using PDV.Domain.Entities;
using PDV.Domain.Enums;

namespace PDV.Domain.Repositories;

public interface IClientRepository : ICrudRepository<Client>
{
    /// <summary>Busca clientes por nombre (coincidencia parcial o exacta).</summary>
    Task<List<Client>> GetByNameAsync(string name, CancellationToken cancellationToken = default);
    
    /// <summary>Busca clientes por teléfono.</summary>
    Task<List<Client>> GetByPhoneAsync(string phone, CancellationToken cancellationToken = default);
    
    /// <summary>Busca un cliente por su RFC (TaxId).</summary>
    Task<Client?> GetByRfcAsync(string rfc, CancellationToken cancellationToken = default);

    /// <summary>Busca clientes por tipo (Mayorista/Menudeo).</summary>
    Task<List<Client>> GetByClientTypeAsync(ClientType clientType, CancellationToken cancellationToken = default);
    
    /// <summary>Obtiene todos los clientes activos.</summary>
    Task<List<Client>> GetAllActiveAsync(CancellationToken cancellationToken = default);
    
    /// <summary>Obtiene todos los clientes inactivos.</summary>
    Task<List<Client>> GetAllInactiveAsync(CancellationToken cancellationToken = default);
}


