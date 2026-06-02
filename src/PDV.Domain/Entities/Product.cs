using PDV.Domain.Common;
using PDV.Domain.Enums;
using PDV.Domain.Events;
using PDV.Domain.Exceptions;

namespace PDV.Domain.Entities;

/// <summary>
/// Agregado raíz que representa un Producto del catálogo.
/// </summary>
public class Product : BaseEntity, IAggregateRoot
{
    private readonly List<InventoryMovement> _movements = new();
    /// <summary>Movimientos de inventario cargados desde la base de datos. No incluye movimientos pendientes de guardado.</summary>
    public IReadOnlyCollection<InventoryMovement> Movements => _movements.AsReadOnly();

    public string Name { get; private set; }
    public string Code { get; private set; }
    public string? Plu { get; private set; }        // Price Look-Up code (balanza/scanner)
    public string? Barcode { get; private set; }    // Código de barras EAN-13 / UPC-A
    public string? Description { get; private set; }

    public decimal Price { get; private set; }
    public decimal? WholesalePrice { get; private set; }            // Precio a mayoreo (Price2)
    public decimal? WholesaleMinQuantity { get; private set; }     // Cantidad mínima para mayoreo
    public decimal Cost { get; private set; }       // Costo de adquisición (para margen)

    public decimal Stock { get; private set; }      // decimal para soportar granel (kg/lt)
    public decimal MinStock { get; private set; }   // Stock mínimo para alerta de reorden

    public string Category { get; private set; }
    public SaleType SaleType { get; private set; }
    public TaxRateType TaxRate { get; private set; }
    public bool IsActive { get; private set; }

    public string SatCode { get; private set; } = string.Empty;
    public ProductType Type { get; private set; } = ProductType.Producto;
    public ControlExistencia ControlExistencia { get; private set; } = ControlExistencia.ConControl;
    public int? SaleUnitId { get; private set; } = 0;
    public string SaleUnitName { get; private set; } = "Pieza";
    public int? XmlUnitId { get; private set; } = 0;
    public string Department { get; private set; } = string.Empty;
    public int? Clasificacion1Id { get; private set; }
    public int? Clasificacion5Id { get; private set; }

    public Guid? BranchId { get; private set; }
    public Branch? Branch { get; private set; }

    /// <summary>Token de concurrencia para evitar actualizaciones de stock simultáneas.</summary>
    public byte[]? RowVersion { get; set; }

#pragma warning disable CS8618
    private Product() { } // Para EF Core
#pragma warning restore CS8618

    public Product(
        string name,
        string code,
        decimal price,
        decimal stock = 0,
        SaleType saleType = SaleType.Piece,
        TaxRateType taxRate = TaxRateType.Rate16,
        string category = "",
        decimal cost = 0,
        decimal minStock = 0,
        string? plu = null,
        string? barcode = null,
        string? description = null,
        Guid? branchId = null,
        decimal? wholesalePrice = null,
        decimal? wholesaleMinQuantity = null,
        string satCode = "",
        ProductType type = ProductType.Producto,
        ControlExistencia controlExistencia = ControlExistencia.ConControl,
        int? saleUnitId = 0,
        string saleUnitName = "Pieza",
        int? xmlUnitId = 0,
        string department = "",
        int? clasificacion1Id = null,
        int? clasificacion5Id = null)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new DomainException("El nombre del producto es requerido.");
        if (string.IsNullOrWhiteSpace(code)) throw new DomainException("El código del producto es requerido.");
        if (price < 0) throw new DomainException("El precio no puede ser negativo.");
        if (cost < 0) throw new DomainException("El costo no puede ser negativo.");
        if (stock < 0) throw new DomainException("El stock inicial no puede ser negativo.");
        if (minStock < 0) throw new DomainException("El stock mínimo no puede ser negativo.");
        if (wholesalePrice.HasValue && wholesalePrice.Value < 0) throw new DomainException("El precio de mayoreo no puede ser negativo.");
        if (wholesaleMinQuantity.HasValue && wholesaleMinQuantity.Value <= 0) throw new DomainException("La cantidad mínima de mayoreo debe ser mayor a cero.");

        Name = name.Trim();
        Code = code.Trim();
        Price = price;
        Cost = cost;
        Stock = stock;
        MinStock = minStock;
        SaleType = saleType;
        TaxRate = taxRate;
        Category = category?.Trim() ?? string.Empty;
        Plu = plu?.Trim();
        Barcode = barcode?.Trim();
        Description = description?.Trim();
        BranchId = branchId;
        WholesalePrice = wholesalePrice;
        WholesaleMinQuantity = wholesaleMinQuantity;
        IsActive = true;

        SatCode = satCode?.Trim() ?? string.Empty;
        Type = type;
        ControlExistencia = controlExistencia;
        SaleUnitId = saleUnitId;
        SaleUnitName = saleUnitName?.Trim() ?? "Pieza";
        XmlUnitId = xmlUnitId;
        Department = department?.Trim() ?? string.Empty;
        Clasificacion1Id = clasificacion1Id;
        Clasificacion5Id = clasificacion5Id;

        AddDomainEvent(new ProductCreatedEvent(Id, Name, Code));
    }

    // ──────────────────────────────────────────────
    // Precio
    // ──────────────────────────────────────────────

    public void UpdatePrice(decimal newPrice)
    {
        if (newPrice < 0) throw new DomainException("El precio no puede ser negativo.");

        var oldPrice = Price;
        Price = newPrice;

        AddDomainEvent(new ProductPriceUpdatedEvent(Id, oldPrice, Price));
    }

    public void UpdateWholesalePrice(decimal? price, decimal? minQuantity)
    {
        if (price.HasValue && price.Value < 0) throw new DomainException("El precio de mayoreo no puede ser negativo.");
        if (minQuantity.HasValue && minQuantity.Value <= 0) throw new DomainException("La cantidad mínima de mayoreo debe ser mayor a cero.");

        WholesalePrice = price;
        WholesaleMinQuantity = minQuantity;
    }

    // ──────────────────────────────────────────────
    // Stock
    // ──────────────────────────────────────────────

    public void ReduceStock(decimal quantity)
    {
        if (quantity <= 0) throw new DomainException("La cantidad a reducir debe ser mayor a cero.");
        if (Stock < quantity) throw new DomainException($"Stock insuficiente. Disponible: {Stock}, Requerido: {quantity}.");

        Stock -= quantity;
        AddDomainEvent(new ProductStockReducedEvent(Id, (int)quantity, (int)Stock));
    }

    public void IncreaseStock(decimal quantity)
    {
        if (quantity <= 0) throw new DomainException("La cantidad a aumentar debe ser mayor a cero.");

        Stock += quantity;
        AddDomainEvent(new ProductStockIncreasedEvent(Id, (int)quantity, (int)Stock));
    }

    /// <summary>
    /// Ajuste manual de inventario (conteo físico, auditoría).
    /// </summary>
    public void AdjustStock(decimal newStock)
    {
        if (newStock < 0) throw new DomainException("El stock ajustado no puede ser negativo.");

        var oldStock = Stock;
        Stock = newStock;
        AddDomainEvent(new ProductStockAdjustedEvent(Id, (int)oldStock, (int)Stock));
    }

    public bool HasStock(decimal quantity)
        => quantity > 0 && Stock >= quantity;

    public bool IsLowStock()
        => MinStock > 0 && Stock <= MinStock;

    // ──────────────────────────────────────────────
    // Información general
    // ──────────────────────────────────────────────

    public void UpdateInfo(
        string name, 
        string? description = null, 
        string? category = null,
        string? satCode = null,
        ProductType? type = null,
        ControlExistencia? controlExistencia = null,
        int? saleUnitId = null,
        string? saleUnitName = null,
        int? xmlUnitId = null,
        string? department = null,
        int? clasificacion1Id = null,
        int? clasificacion5Id = null)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new DomainException("El nombre del producto es requerido.");

        Name = name.Trim();
        Description = description?.Trim();
        Category = category?.Trim() ?? Category;

        SatCode = satCode?.Trim() ?? SatCode;
        Type = type ?? Type;
        ControlExistencia = controlExistencia ?? ControlExistencia;
        SaleUnitId = saleUnitId ?? SaleUnitId;
        SaleUnitName = saleUnitName?.Trim() ?? SaleUnitName;
        XmlUnitId = xmlUnitId ?? XmlUnitId;
        Department = department?.Trim() ?? Department;
        Clasificacion1Id = clasificacion1Id ?? Clasificacion1Id;
        Clasificacion5Id = clasificacion5Id ?? Clasificacion5Id;

        AddDomainEvent(new ProductInfoUpdatedEvent(Id, Name, Category));
    } // ──────────────────────────────────────────────


    public void ChangeCode(string newCode)
    {
        if (string.IsNullOrWhiteSpace(newCode)) throw new DomainException("El código del producto es requerido.");

        var oldCode = Code;
        Code = newCode.Trim();
        AddDomainEvent(new ProductCodeChangedEvent(Id, oldCode, Code));
    }

    public void UpdatePlu(string? plu) => Plu = plu?.Trim();
    public void UpdateBarcode(string? barcode) => Barcode = barcode?.Trim();

    public void UpdateCost(decimal newCost)
    {
        if (newCost < 0) throw new DomainException("El costo no puede ser negativo.");
        Cost = newCost;
    }

    public void UpdateMinStock(decimal newMinStock)
    {
        if (newMinStock < 0) throw new DomainException("El stock mínimo no puede ser negativo.");
        MinStock = newMinStock;
    }

    public void UpdateTaxRate(TaxRateType newTaxRate)
    {
        TaxRate = newTaxRate;
    }

    // ──────────────────────────────────────────────
    // Estado
    // ──────────────────────────────────────────────

    public void Activate()
    {
        if (IsActive) throw new DomainException("El producto ya está activo.");
        IsActive = true;
        AddDomainEvent(new ProductActivatedEvent(Id));
    }

    public void Deactivate()
    {
        if (!IsActive) throw new DomainException("El producto ya está inactivo.");
        IsActive = false;
        AddDomainEvent(new ProductDeactivatedEvent(Id));
    }

    public void ChangeSaleType(SaleType saleType)
    {
        SaleType = saleType;
    }

    public void ApplyMovement(decimal quantity, InventoryMovementType type, Guid? referenceId = null, string? remarks = null)
    {
        if (quantity == 0)
            throw new DomainException("La cantidad del movimiento no puede ser cero.");

        // Actualizar la caché de lectura del stock
        Stock += quantity;

        // Levantar el evento — AppDbContext creará y persistirá el InventoryMovement
        var movementId = Guid.CreateVersion7();
        AddDomainEvent(new InventoryMovementRegisteredEvent(movementId, Id, quantity, type, referenceId, remarks));
    }
}
