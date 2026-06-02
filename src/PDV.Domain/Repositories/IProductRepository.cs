using PDV.Domain.Entities;
using PDV.Domain.Enums;

namespace PDV.Domain.Repositories;

/// <summary>
/// Repositorio para Product
/// </summary>
public interface IProductRepository : ICrudRepository<Product>
{
    /// <summary>Obtiene un producto por su código interno.</summary>
    Task<Product?> GetByCodeAsync(string code, CancellationToken cancellationToken = default);

    /// <summary>Busca productos por nombre (coincidencia parcial o exacta).</summary>
    Task<List<Product>> GetByNameAsync(string name, CancellationToken cancellationToken = default);

    /// <summary>Obtiene un producto por su código PLU de balanza.</summary>
    Task<Product?> GetByPluAsync(string plu, CancellationToken cancellationToken = default);

    /// <summary>Obtiene un producto por su código de barras (EAN/UPC).</summary>
    Task<Product?> GetByBarcodeAsync(string barcode, CancellationToken cancellationToken = default);

    /// <summary>Busca productos por descripción (coincidencia parcial).</summary>
    Task<List<Product>> GetByDescriptionAsync(string description, CancellationToken cancellationToken = default);

    /// <summary>Busca productos por categoría.</summary>
    Task<List<Product>> GetByCategoryAsync(string category, CancellationToken cancellationToken = default);

    /// <summary>Obtiene productos filtrados por tipo de venta (Granel/Pieza).</summary>
    Task<List<Product>> GetBySaleTypeAsync(SaleType saleType, CancellationToken cancellationToken = default);

    /// <summary>Obtiene productos filtrados por tasa de impuesto.</summary>
    Task<List<Product>> GetByTaxRateAsync(TaxRateType taxRate, CancellationToken cancellationToken = default);

    /// <summary>Obtiene todos los productos activos.</summary>
    Task<List<Product>> GetAllActiveAsync(CancellationToken cancellationToken = default);

    /// <summary>Obtiene todos los productos inactivos.</summary>
    Task<List<Product>> GetAllInactiveAsync(CancellationToken cancellationToken = default);

    /// <summary>Obtiene los productos asociados a una sucursal específica (incluye los globales).</summary>
    Task<List<Product>> GetByBranchIdAsync(Guid branchId, CancellationToken cancellationToken = default);

    /// <summary>Busca productos por múltiples criterios opcionales.</summary>
    Task<List<Product>> GetByCriteriaAsync(
        string? name, 
        string? code, 
        string? plu, 
        string? barcode, 
        string? category, 
        SaleType? saleType, 
        TaxRateType? taxRate, 
        bool? isActive, 
        Guid? branchId, 
        CancellationToken cancellationToken = default);
}


