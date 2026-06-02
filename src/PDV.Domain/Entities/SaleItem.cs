using PDV.Domain.Common;
using PDV.Domain.Enums;
using PDV.Domain.Exceptions;

namespace PDV.Domain.Entities;

public class SaleItem : BaseEntity
{
    private decimal _unitPrice;
    private decimal? _priceOverride;

    public Guid SaleId { get; private set; }
    public Sale Sale { get; private set; } = null!;

    public Guid ProductId { get; private set; }
    public Product Product { get; private set; } = null!;

    public string ProductName { get; private set; } = string.Empty; // Snapshot name
    
    public decimal UnitPrice 
    { 
        get => _unitPrice; 
        private set => _unitPrice = value;
    }

    public decimal? PriceOverride 
    { 
        get => _priceOverride; 
        private set => _priceOverride = value;
    }

    /// <summary>decimal para soportar productos a granel. Piezas siempre serán entero sin fracción.</summary>
    public decimal Quantity { get; private set; }
    public decimal TaxRate { get; private set; }
    public bool IsTaxExempt { get; private set; }
    public bool IsReturned { get; private set; }

    // Calculados
    public decimal Subtotal => Quantity * UnitPrice;
    public decimal TotalTax => IsTaxExempt ? 0 : Subtotal * (TaxRate / 100m);
    public decimal TotalAmount => Subtotal + TotalTax;

#pragma warning disable CS8618
    private SaleItem() { } // For EF Core
#pragma warning restore CS8618

    public SaleItem(
        Product product,
        decimal quantity,
        decimal taxRate,
        bool isTaxExempt = false,
        decimal? priceOverride = null)
    {
        if (product == null) throw new DomainException("El producto es obligatorio.");
        ValidateQuantity(quantity, product.SaleType);
        if (taxRate < 0) throw new DomainException("La tasa de impuesto no puede ser negativa.");
        if (isTaxExempt && taxRate > 0) throw new DomainException("Un producto exento no puede tener una tasa de impuesto mayor a cero.");

        ProductId = product.Id;
        Product = product;
        ProductName = product.Name;
        Quantity = quantity;
        TaxRate = taxRate;
        IsTaxExempt = isTaxExempt;

        _unitPrice = product.Price;
        if (product.WholesalePrice.HasValue && product.WholesaleMinQuantity.HasValue && quantity >= product.WholesaleMinQuantity.Value)
        {
            _unitPrice = product.WholesalePrice.Value;
        }
        
        if (priceOverride.HasValue)
        {
            if (priceOverride.Value < 0) throw new DomainException("El precio sobrescrito no puede ser negativo.");

            _priceOverride = priceOverride.Value;
            _unitPrice = _priceOverride.Value;
        }
    }

    internal void SetSale(Sale sale)
    {
        Sale = sale ?? throw new DomainException("La venta asignada no puede ser nula.");
        SaleId = sale.Id;
    }

    public void UpdateQuantity(decimal newQuantity)
    {
        ValidateQuantity(newQuantity, Product!.SaleType);
        Quantity = newQuantity;

        if (!_priceOverride.HasValue)
        {
            if (Product.WholesalePrice.HasValue && Product.WholesaleMinQuantity.HasValue && Quantity >= Product.WholesaleMinQuantity.Value)
            {
                _unitPrice = Product.WholesalePrice.Value;
            }
            else
            {
                _unitPrice = Product.Price;
            }
        }
    }

    public void OverridePrice(decimal newPrice)
    {
        if (newPrice < 0) throw new DomainException("El precio no puede ser negativo.");

        _priceOverride = newPrice;
        _unitPrice = _priceOverride.Value;
    }

    public void MarkAsReturned()
    {
        IsReturned = true;
    }

    public decimal GetEffectivePrice() => _unitPrice;

    /// <summary>
    /// Valida que la cantidad sea válida según el tipo de venta del producto.
    /// Productos por Pieza: solo cantidades enteras (sin fracción).
    /// Productos a Granel: se permiten decimales.
    /// </summary>
    private static void ValidateQuantity(decimal quantity, SaleType saleType)
    {
        if (quantity <= 0)
            throw new DomainException("La cantidad debe ser mayor a cero.");

        if (saleType == SaleType.Piece && quantity != Math.Floor(quantity))
            throw new DomainException(
                $"El producto se vende por pieza. La cantidad debe ser un número entero, no '{quantity}'.");
    }
}

