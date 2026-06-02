using PDV.Domain.Entities;
using PDV.Domain.Enums;

namespace PDV.Domain.Repositories;

public interface IPrinterRepository : ICrudRepository<Printer>
{
    /// <summary>Obtiene las impresoras asociadas a una sucursal específica.</summary>
    Task<List<Printer>> GetByBranchIdAsync(int branchId, CancellationToken cancellationToken = default);

    /// <summary>Obtiene impresoras filtradas por su tipo de conexión.</summary>
    Task<List<Printer>> GetByConnectionTypeAsync(PrinterConnectionType connectionType, CancellationToken cancellationToken = default);

    /// <summary>Obtiene todas las impresoras activas.</summary>
    Task<List<Printer>> GetAllActiveAsync(CancellationToken cancellationToken = default);

    /// <summary>Obtiene todas las impresoras inactivas.</summary>
    Task<List<Printer>> GetAllInactiveAsync(CancellationToken cancellationToken = default);

    /// <summary>Obtiene una impresora por su dirección IP.</summary>
    Task<Printer?> GetByIpAddressAsync(string ipAddress, CancellationToken cancellationToken = default);

    /// <summary>Obtiene las impresoras activas que no están asignadas a ninguna caja registradora.</summary>
    Task<List<Printer>> GetAvailablePrintersAsync(CancellationToken cancellationToken = default);
}
