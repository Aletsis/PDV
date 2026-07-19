using System;
using PDV.Domain.Entities;
using PDV.Domain.Enums;
using PDV.Domain.Exceptions;
using PDV.Domain.Services;
using Xunit;

namespace PDV.Tests.Domain.Services;

public class SaleDomainServiceTests
{
    [Fact]
    public void ApplyDiscount_WithInvalidPercentage_ThrowsDomainException()
    {
        // Arrange
        var service = new SaleDomainService();
        var sale = new Sale("T-9001", PaymentMethodType.Cash, "user-123", Guid.NewGuid());

        // Act & Assert - Porcentaje negativo
        var exception1 = Assert.Throws<DomainException>(() => service.ApplyDiscount(sale, -5m));
        Assert.Equal("El porcentaje de descuento debe estar entre 0 y 100.", exception1.Message);

        // Act & Assert - Porcentaje mayor a 100
        var exception2 = Assert.Throws<DomainException>(() => service.ApplyDiscount(sale, 105m));
        Assert.Equal("El porcentaje de descuento debe estar entre 0 y 100.", exception2.Message);
    }

    [Fact]
    public void ApplyDiscount_ToAlreadyPaidSale_ThrowsDomainException()
    {
        // Arrange
        var service = new SaleDomainService();
        var sale = new Sale("T-9002", PaymentMethodType.Cash, "user-123", Guid.NewGuid());
        
        // Agregar un item para permitir marcar como pagada
        var product = new Product("Refresco", "REF-01", 20m);
        var item = new SaleItem(product, 1m, 16m);
        sale.AddItem(item);
        sale.MarkAsPaid();

        // Act & Assert
        var exception = Assert.Throws<DomainException>(() => service.ApplyDiscount(sale, 10m));
        Assert.Equal("No se puede aplicar un descuento a una venta ya pagada.", exception.Message);
    }

    [Fact]
    public void ApplyDiscount_WithNullSale_ThrowsArgumentNullException()
    {
        // Arrange
        var service = new SaleDomainService();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => service.ApplyDiscount(null!, 10m));
    }
}
