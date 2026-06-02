using PDV.Domain.Entities;
using PDV.Domain.Enums;

namespace PDV.Domain.Repositories;

public interface IEmployeeRepository : ICrudRepository<Employee>
{
    /// <summary>Obtiene un empleado por su código.</summary>
    Task<Employee?> GetByCodeAsync(string employeeCode, CancellationToken cancellationToken = default);
    
    /// <summary>Obtiene un empleado por su ID de usuario del sistema.</summary>
    Task<Employee?> GetByUserIdAsync(string userId, CancellationToken cancellationToken = default);
    
    /// <summary>Obtiene todos los empleados (opcionalmente incluye inactivos).</summary>
    Task<List<Employee>> GetAllAsync(bool includeInactive = false, CancellationToken cancellationToken = default);

    /// <summary>Obtiene todos los empleados activos.</summary>
    Task<List<Employee>> GetAllActiveAsync(CancellationToken cancellationToken = default);

    /// <summary>Obtiene todos los empleados inactivos.</summary>
    Task<List<Employee>> GetAllInactiveAsync(CancellationToken cancellationToken = default);

    /// <summary>Obtiene empleados por rol de empleado.</summary>
    Task<List<Employee>> GetByRoleAsync(EmployeeRole role, CancellationToken cancellationToken = default);
}


