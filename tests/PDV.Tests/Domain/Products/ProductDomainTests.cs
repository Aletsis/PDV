using System;
using System.Linq;
using PDV.Domain.Entities;
using PDV.Domain.Enums;
using PDV.Domain.Events;
using PDV.Domain.Exceptions;
using Xunit;

namespace PDV.Tests.Domain.Products;

public class ProductDomainTests
{
    [Fact]
    public void Constructor_WithValidParameters_InitializesCorrectlyAndRaisesEvent()
    {
        // Act
        var product = new Product(
            name: "Coca Cola 600ml",
            code: "CC-600",
            price: 18.50m,
            saleType: SaleType.Piece,
            taxRate: TaxRateType.Rate16,
            category: "Refrescos",
            cost: 12.00m
        );

        // Assert
        Assert.Equal("Coca Cola 600ml", product.Name);
        Assert.Equal("CC-600", product.Code);
        Assert.Equal(18.50m, product.Price);
        Assert.Equal(SaleType.Piece, product.SaleType);
        Assert.Equal(TaxRateType.Rate16, product.TaxRate);
        Assert.Equal("Refrescos", product.Category);
        Assert.Equal(12.00m, product.Cost);
        Assert.True(product.IsActive);

        // Verify Domain Event
        var createdEvent = product.DomainEvents.OfType<ProductCreatedEvent>().FirstOrDefault();
        Assert.NotNull(createdEvent);
        Assert.Equal(product.Id, createdEvent!.ProductId);
        Assert.Equal(product.Name, createdEvent.Name);
        Assert.Equal(product.Code, createdEvent.Code);
    }

    [Theory]
    [InlineData("", "CC-600", 10, "El nombre del producto es requerido.")]
    [InlineData("Coca Cola", "", 10, "El código del producto es requerido.")]
    [InlineData("Coca Cola", "CC-600", -1, "El precio no puede ser negativo.")]
    public void Constructor_WithInvalidParameters_ThrowsDomainException(string name, string code, decimal price, string expectedMessage)
    {
        // Act & Assert
        var exception = Assert.Throws<DomainException>(() => new Product(
            name: name,
            code: code,
            price: price
        ));
        Assert.Equal(expectedMessage, exception.Message);
    }

    [Fact]
    public void UpdatePrice_WithValidPrice_UpdatesAndRaisesEvent()
    {
        // Arrange
        var product = new Product("Coca Cola", "CC-600", 18.50m);

        // Act
        product.UpdatePrice(20.00m);

        // Assert
        Assert.Equal(20.00m, product.Price);

        var priceEvent = product.DomainEvents.OfType<ProductPriceUpdatedEvent>().FirstOrDefault();
        Assert.NotNull(priceEvent);
        Assert.Equal(product.Id, priceEvent!.ProductId);
        Assert.Equal(18.50m, priceEvent.OldPrice);
        Assert.Equal(20.00m, priceEvent.NewPrice);
    }

    [Fact]
    public void UpdatePrice_WithNegativePrice_ThrowsDomainException()
    {
        // Arrange
        var product = new Product("Coca Cola", "CC-600", 18.50m);

        // Act & Assert
        var exception = Assert.Throws<DomainException>(() => product.UpdatePrice(-5m));
        Assert.Equal("El precio no puede ser negativo.", exception.Message);
    }

    [Fact]
    public void ActivateAndDeactivate_StateTransitionsCorrectlyAndRaisesEvents()
    {
        // Arrange
        var product = new Product("Coca Cola", "CC-600", 18.50m);
        Assert.True(product.IsActive);

        // Act - Deactivate
        product.Deactivate();

        // Assert
        Assert.False(product.IsActive);
        var deactivatedEvent = product.DomainEvents.OfType<ProductDeactivatedEvent>().FirstOrDefault();
        Assert.NotNull(deactivatedEvent);
        Assert.Equal(product.Id, deactivatedEvent!.ProductId);

        // Act - Activate
        product.Activate();

        // Assert
        Assert.True(product.IsActive);
        var activatedEvent = product.DomainEvents.OfType<ProductActivatedEvent>().FirstOrDefault();
        Assert.NotNull(activatedEvent);
        Assert.Equal(product.Id, activatedEvent!.ProductId);
    }
}
