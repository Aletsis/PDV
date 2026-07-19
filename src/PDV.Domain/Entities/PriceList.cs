using System;
using System.Collections.Generic;
using PDV.Domain.Common;
using PDV.Domain.Exceptions;

namespace PDV.Domain.Entities;

public class PriceList : BaseEntity, IAggregateRoot
{
    public string Name { get; private set; }
    public string? Description { get; private set; }
    public bool IsActive { get; private set; }

    private readonly List<PriceListProduct> _productPrices = new();
    public IReadOnlyCollection<PriceListProduct> ProductPrices => _productPrices.AsReadOnly();

#pragma warning disable CS8618
    private PriceList() { }
#pragma warning restore CS8618

    public PriceList(string name, string? description = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("El nombre de la lista de precios es requerido.");

        Name = name.Trim();
        Description = description?.Trim();
        IsActive = true;
    }

    public void Update(string name, string? description)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("El nombre es requerido.");

        Name = name.Trim();
        Description = description?.Trim();
    }

    public void AddOrUpdatePrice(Guid productId, decimal price)
    {
        if (price < 0)
            throw new DomainException("El precio no puede ser negativo.");

        var existing = _productPrices.Find(p => p.ProductId == productId);
        if (existing != null)
        {
            existing.UpdatePrice(price);
        }
        else
        {
            _productPrices.Add(new PriceListProduct(Id, productId, price));
        }
    }

    public void RemovePrice(Guid productId)
    {
        var existing = _productPrices.Find(p => p.ProductId == productId);
        if (existing != null)
        {
            _productPrices.Remove(existing);
        }
    }

    public void Activate() => IsActive = true;
    public void Deactivate() => IsActive = false;
}
