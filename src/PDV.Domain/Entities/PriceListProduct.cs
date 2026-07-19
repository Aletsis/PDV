using System;
using PDV.Domain.Exceptions;

namespace PDV.Domain.Entities;

public class PriceListProduct
{
    public Guid PriceListId { get; private set; }
    public Guid ProductId { get; private set; }
    public decimal Price { get; private set; }
    
    public Product Product { get; private set; } = null!;

#pragma warning disable CS8618
    private PriceListProduct() { }
#pragma warning restore CS8618

    public PriceListProduct(Guid priceListId, Guid productId, decimal price)
    {
        if (price < 0)
            throw new DomainException("El precio no puede ser negativo.");

        PriceListId = priceListId;
        ProductId = productId;
        Price = price;
    }

    internal void UpdatePrice(decimal newPrice)
    {
        if (newPrice < 0)
            throw new DomainException("El precio no puede ser negativo.");
        Price = newPrice;
    }
}
