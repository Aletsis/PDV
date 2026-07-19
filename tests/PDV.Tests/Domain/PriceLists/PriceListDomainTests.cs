using System;
using System.Linq;
using PDV.Domain.Entities;
using PDV.Domain.Exceptions;
using Xunit;

namespace PDV.Tests.Domain.PriceLists;

public class PriceListDomainTests
{
    [Fact]
    public void Constructor_WithValidParameters_InitializesCorrectly()
    {
        // Act
        var priceList = new PriceList("Lista Sucursal A", "Precios asignados para la sucursal de la zona A.");

        // Assert
        Assert.Equal("Lista Sucursal A", priceList.Name);
        Assert.Equal("Precios asignados para la sucursal de la zona A.", priceList.Description);
        Assert.True(priceList.IsActive);
        Assert.Empty(priceList.ProductPrices);
    }

    [Fact]
    public void Constructor_WithEmptyName_ThrowsDomainException()
    {
        // Act & Assert
        var exception = Assert.Throws<DomainException>(() => new PriceList(""));
        Assert.Equal("El nombre de la lista de precios es requerido.", exception.Message);
    }

    [Fact]
    public void Update_WithValidParameters_UpdatesCorrectly()
    {
        // Arrange
        var priceList = new PriceList("Original Name", "Original Desc");

        // Act
        priceList.Update("Updated Name", "Updated Desc");

        // Assert
        Assert.Equal("Updated Name", priceList.Name);
        Assert.Equal("Updated Desc", priceList.Description);
    }

    [Fact]
    public void AddOrUpdatePrice_NewProduct_AddsPriceToList()
    {
        // Arrange
        var priceList = new PriceList("Lista Test");
        var productId = Guid.NewGuid();

        // Act
        priceList.AddOrUpdatePrice(productId, 49.90m);

        // Assert
        Assert.Single(priceList.ProductPrices);
        var productPrice = priceList.ProductPrices.First();
        Assert.Equal(productId, productPrice.ProductId);
        Assert.Equal(49.90m, productPrice.Price);
    }

    [Fact]
    public void AddOrUpdatePrice_ExistingProduct_UpdatesPriceInList()
    {
        // Arrange
        var priceList = new PriceList("Lista Test");
        var productId = Guid.NewGuid();
        priceList.AddOrUpdatePrice(productId, 49.90m);

        // Act
        priceList.AddOrUpdatePrice(productId, 45.00m);

        // Assert
        Assert.Single(priceList.ProductPrices);
        var productPrice = priceList.ProductPrices.First();
        Assert.Equal(45.00m, productPrice.Price);
    }

    [Fact]
    public void AddOrUpdatePrice_NegativePrice_ThrowsDomainException()
    {
        // Arrange
        var priceList = new PriceList("Lista Test");
        var productId = Guid.NewGuid();

        // Act & Assert
        var exception = Assert.Throws<DomainException>(() => priceList.AddOrUpdatePrice(productId, -10.00m));
        Assert.Equal("El precio no puede ser negativo.", exception.Message);
    }

    [Fact]
    public void RemovePrice_ExistingProduct_RemovesFromList()
    {
        // Arrange
        var priceList = new PriceList("Lista Test");
        var productId = Guid.NewGuid();
        priceList.AddOrUpdatePrice(productId, 49.90m);
        Assert.Single(priceList.ProductPrices);

        // Act
        priceList.RemovePrice(productId);

        // Assert
        Assert.Empty(priceList.ProductPrices);
    }

    [Fact]
    public void ActivateAndDeactivate_StateTransitionsCorrectly()
    {
        // Arrange
        var priceList = new PriceList("Lista Test");
        Assert.True(priceList.IsActive);

        // Act - Deactivate
        priceList.Deactivate();
        Assert.False(priceList.IsActive);

        // Act - Activate
        priceList.Activate();
        Assert.True(priceList.IsActive);
    }
}
