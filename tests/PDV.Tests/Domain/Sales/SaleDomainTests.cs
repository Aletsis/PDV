using System;
using System.Linq;
using PDV.Domain.Entities;
using PDV.Domain.Enums;
using PDV.Domain.Events;
using PDV.Domain.Exceptions;
using PDV.Domain.ValueObjects;
using Xunit;

namespace PDV.Tests.Domain.Sales;

public class SaleDomainTests
{
    private Product CreateProduct(string name, decimal price, SaleType saleType = SaleType.Piece, decimal? wholesalePrice = null, decimal? wholesaleMinQty = null)
    {
        return new Product(
            name: name,
            code: Guid.NewGuid().ToString().Substring(0, 8),
            price: price,
            saleType: saleType,
            wholesalePrice: wholesalePrice,
            wholesaleMinQuantity: wholesaleMinQty
        );
    }

    [Fact]
    public void Constructor_WithValidParameters_InitializesCorrectlyAndRaisesEvent()
    {
        // Arrange
        var saleNumber = SaleNumber.Create("T-1001");
        var shiftId = Guid.NewGuid();
        var userId = "user-123";

        // Act
        var sale = new Sale(saleNumber, PaymentMethodType.Cash, userId, shiftId);

        // Assert
        Assert.Equal("T-1001", sale.SaleNumber);
        Assert.Equal(PaymentMethodType.Cash, sale.PaymentMethod);
        Assert.Equal(userId, sale.UserId);
        Assert.Equal(shiftId, sale.ShiftId);
        Assert.False(sale.IsPaid);
        Assert.False(sale.IsCancelled);
        Assert.Equal(0m, sale.Subtotal);
        Assert.Equal(0m, sale.TotalTax);
        Assert.Equal(0m, sale.TotalAmount);

        var createdEvent = sale.DomainEvents.OfType<SaleCreatedEvent>().FirstOrDefault();
        Assert.NotNull(createdEvent);
        Assert.Equal(sale.Id, createdEvent!.SaleId);
    }

    [Fact]
    public void RecalculateTotals_WithMixedTaxesItems_CalculatesSubtotalAndTaxBreakdown()
    {
        // Arrange
        var sale = new Sale("T-1002", PaymentMethodType.Cash, "user-123", Guid.NewGuid());
        var prodTaxable = CreateProduct("Refresco", 20m); // 20 * 3 = 60
        var prodExempt = CreateProduct("Pan", 10m);       // 10 * 2 = 20

        var item1 = new SaleItem(prodTaxable, 3m, 16m, isTaxExempt: false);
        var item2 = new SaleItem(prodExempt, 2m, 0m, isTaxExempt: true);

        // Act
        sale.AddItem(item1);
        sale.AddItem(item2);

        // Assert
        // Subtotal = 20*3 + 10*2 = 80
        Assert.Equal(80m, sale.Subtotal);

        // Taxes breakdown check:
        // Taxable: Base = 60, TaxAmount = 60 * 0.16 = 9.60
        // Exempt: Base = 20, TaxAmount = 0
        Assert.Equal(2, sale.Taxes.Count);
        
        var tax16 = sale.Taxes.FirstOrDefault(t => t.Rate == 16m);
        Assert.NotNull(tax16);
        Assert.Equal(60m, tax16!.BaseAmount);
        Assert.Equal(9.60m, tax16.TaxAmount);
        Assert.False(tax16.IsExempt);

        var taxExempt = sale.Taxes.FirstOrDefault(t => t.IsExempt);
        Assert.NotNull(taxExempt);
        Assert.Equal(20m, taxExempt!.BaseAmount);
        Assert.Equal(0m, taxExempt.TaxAmount);

        Assert.Equal(9.60m, sale.TotalTax);
        Assert.Equal(89.60m, sale.TotalAmount);
    }

    [Fact]
    public void AddItem_ToPaidOrCancelledSale_ThrowsDomainException()
    {
        // Arrange
        var sale = new Sale("T-1003", PaymentMethodType.Cash, "user-123", Guid.NewGuid());
        var prod = CreateProduct("Refresco", 20m);
        var item = new SaleItem(prod, 1m, 16m);
        sale.AddItem(item);
        sale.MarkAsPaid();

        // Act & Assert
        var exception = Assert.Throws<DomainException>(() => sale.AddItem(new SaleItem(prod, 1m, 16m)));
        Assert.Equal("No se pueden agregar artículos a una venta pagada.", exception.Message);

        var sale2 = new Sale("T-1004", PaymentMethodType.Cash, "user-123", Guid.NewGuid());
        sale2.Cancel("Motivo de prueba");
        
        var exception2 = Assert.Throws<DomainException>(() => sale2.AddItem(new SaleItem(prod, 1m, 16m)));
        Assert.Equal("No se pueden agregar artículos a una venta cancelada.", exception2.Message);
    }

    [Fact]
    public void SaleItem_Constructor_WithInvalidQuantityForPieceType_ThrowsDomainException()
    {
        // Arrange
        var prodPiece = CreateProduct("Gansito", 15m, SaleType.Piece);
        var prodBulk = CreateProduct("Jamon", 120m, SaleType.Bulk);

        // Act & Assert
        var exception = Assert.Throws<DomainException>(() => new SaleItem(prodPiece, 1.5m, 16m));
        Assert.Contains("El producto se vende por pieza. La cantidad debe ser un número entero", exception.Message);

        // Jamon should succeed with decimal quantity
        var jamonItem = new SaleItem(prodBulk, 1.345m, 0m, isTaxExempt: true);
        Assert.Equal(1.345m, jamonItem.Quantity);
    }

    [Fact]
    public void SaleItem_Constructor_AppliesWholesalePriceAutomatically()
    {
        // Arrange
        var prodWithWholesale = CreateProduct("Agua 1L", price: 10m, wholesalePrice: 8m, wholesaleMinQty: 6m);

        // Act - Menor al mínimo de mayoreo
        var itemRetail = new SaleItem(prodWithWholesale, 5m, 16m);
        // Act - Mayor o igual al mínimo de mayoreo
        var itemWholesale = new SaleItem(prodWithWholesale, 6m, 16m);

        // Assert
        Assert.Equal(10m, itemRetail.UnitPrice);
        Assert.Equal(50m, itemRetail.Subtotal);

        Assert.Equal(8m, itemWholesale.UnitPrice);
        Assert.Equal(48m, itemWholesale.Subtotal);
    }

    [Fact]
    public void UpdateItemQuantity_RecalculatesTotalsAndMayoreo()
    {
        // Arrange
        var sale = new Sale("T-1005", PaymentMethodType.Cash, "user-123", Guid.NewGuid());
        var prod = CreateProduct("Agua 1L", price: 10m, wholesalePrice: 8m, wholesaleMinQty: 6m);
        var item = new SaleItem(prod, 2m, 16m); // Retail price (10m)
        sale.AddItem(item);

        Assert.Equal(20m, sale.Subtotal);
        Assert.Equal(10m, item.UnitPrice);

        // Act - Aumentar cantidad sobre el umbral de mayoreo
        sale.UpdateItemQuantity(item.Id, 10m);

        // Assert
        Assert.Equal(8m, item.UnitPrice);
        Assert.Equal(80m, sale.Subtotal);
    }

    [Fact]
    public void Cancel_WithValidReason_CancelsAndRaisesEvent()
    {
        // Arrange
        var sale = new Sale("T-1006", PaymentMethodType.Cash, "user-123", Guid.NewGuid());

        // Act
        sale.Cancel("Cliente se arrepintió");

        // Assert
        Assert.True(sale.IsCancelled);
        var cancelledEvent = sale.DomainEvents.OfType<SaleCancelledEvent>().FirstOrDefault();
        Assert.NotNull(cancelledEvent);
        Assert.Equal(sale.Id, cancelledEvent!.SaleId);
        Assert.Equal("Cliente se arrepintió", cancelledEvent.Reason);
    }
}
