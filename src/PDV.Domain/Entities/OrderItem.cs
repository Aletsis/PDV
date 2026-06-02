using PDV.Domain.Common;
using PDV.Domain.Enums;
using PDV.Domain.Exceptions;

namespace PDV.Domain.Entities;

public class OrderItem : BaseEntity
{
    public Guid ProductId { get; private set; }
    public Product? Product { get; private set; }

    /// <summary>Snapshot del nombre del producto al momento del pedido.</summary>
    public string ProductName { get; private set; } = string.Empty;

    /// <summary>decimal para soportar productos a granel. Piezas siempre serán entero sin fracción.</summary>
    public decimal Quantity { get; private set; }
    public decimal UnitPrice { get; private set; }
    public decimal TaxRate { get; private set; }
    public bool IsTaxExempt { get; private set; }

    // Calculados
    public decimal Subtotal => Quantity * UnitPrice;
    public decimal TotalTax => IsTaxExempt ? 0 : Subtotal * (TaxRate / 100m);
    public decimal TotalAmount => Subtotal + TotalTax;

#pragma warning disable CS8618
    private OrderItem() { } // For EF Core
#pragma warning restore CS8618

    public OrderItem(
        Product product,
        decimal quantity,
        decimal unitPrice,
        decimal taxRate,
        bool isTaxExempt = false)
    {
        if (product == null) throw new DomainException("El producto es obligatorio.");
        ValidateQuantity(quantity, product.SaleType);
        if (unitPrice < 0) throw new DomainException("El precio unitario no puede ser negativo.");
        if (taxRate < 0) throw new DomainException("La tasa de impuesto no puede ser negativa.");
        if (isTaxExempt && taxRate > 0) throw new DomainException("Un producto exento no puede tener una tasa de impuesto mayor a cero.");

        ProductId = product.Id;
        Product = product;
        ProductName = product.Name;
        Quantity = quantity;
        UnitPrice = unitPrice;
        TaxRate = taxRate;
        IsTaxExempt = isTaxExempt;
    }

    public void UpdateQuantity(decimal newQuantity)
    {
        ValidateQuantity(newQuantity, Product!.SaleType);
        Quantity = newQuantity;
    }

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
