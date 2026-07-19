using System;
using System.Linq;
using PDV.Domain.Entities;
using PDV.Domain.Enums;
using PDV.Domain.Events;
using PDV.Domain.Exceptions;
using Xunit;

namespace PDV.Tests.Domain.Sales;

public class ReturnDomainTests
{
    private Product CreateProduct(string name, decimal price, SaleType saleType = SaleType.Piece)
    {
        return new Product(
            name: name,
            code: Guid.NewGuid().ToString().Substring(0, 8),
            price: price,
            saleType: saleType
        );
    }

    [Fact]
    public void Constructor_WithValidParameters_InitializesCorrectlyAndRaisesEvent()
    {
        // Arrange
        var shiftId = Guid.NewGuid();
        var saleId = Guid.NewGuid();

        // Act
        var ret = new Return(
            reason: "Defecto de fábrica",
            refundMethod: RefundMethod.Cash,
            userId: "user-123",
            shiftId: shiftId,
            saleId: saleId
        );

        // Assert
        Assert.Equal("Defecto de fábrica", ret.Reason);
        Assert.Equal(RefundMethod.Cash, ret.RefundMethod);
        Assert.Equal("user-123", ret.UserId);
        Assert.Equal(shiftId, ret.ShiftId);
        Assert.Equal(saleId, ret.SaleId);
        Assert.False(ret.IsCompleted);
        Assert.Equal(0m, ret.Subtotal);
        Assert.Equal(0m, ret.TotalRefund);

        var registeredEvent = ret.DomainEvents.OfType<ReturnRegisteredEvent>().FirstOrDefault();
        Assert.NotNull(registeredEvent);
        Assert.Equal(ret.Id, registeredEvent!.ReturnId);
    }

    [Theory]
    [InlineData("", RefundMethod.Cash, "user-123", "00000000-0000-0000-0000-000000000001", "Se requiere un motivo para la devolución.")]
    [InlineData("Defecto", RefundMethod.Cash, "", "00000000-0000-0000-0000-000000000001", "El ID de usuario es requerido.")]
    [InlineData("Defecto", RefundMethod.Cash, "user-123", "00000000-0000-0000-0000-000000000000", "El ID de turno es requerido para registrar una devolución.")]
    public void Constructor_WithInvalidParameters_ThrowsDomainException(string reason, RefundMethod refundMethod, string userId, string shiftIdString, string expectedMsg)
    {
        // Arrange
        var shiftId = Guid.Parse(shiftIdString);

        // Act & Assert
        var exception = Assert.Throws<DomainException>(() => new Return(reason, refundMethod, userId, shiftId));
        Assert.Equal(expectedMsg, exception.Message);
    }

    [Fact]
    public void AddItem_ToCompletedReturn_ThrowsDomainException()
    {
        // Arrange
        var ret = new Return("Defecto", RefundMethod.Cash, "user-123", Guid.NewGuid());
        var prod = CreateProduct("Refresco", 20m);
        var item = new ReturnItem(prod, 1m, 20m, 16m);
        ret.AddItem(item);
        ret.Complete();

        // Act & Assert
        var exception = Assert.Throws<DomainException>(() => ret.AddItem(new ReturnItem(prod, 1m, 20m, 16m)));
        Assert.Equal("No se pueden agregar ítems a una devolución ya completada.", exception.Message);
    }

    [Fact]
    public void RemoveItem_FromCompletedReturn_ThrowsDomainException()
    {
        // Arrange
        var ret = new Return("Defecto", RefundMethod.Cash, "user-123", Guid.NewGuid());
        var prod = CreateProduct("Refresco", 20m);
        var item = new ReturnItem(prod, 1m, 20m, 16m);
        ret.AddItem(item);
        ret.Complete();

        // Act & Assert
        var exception = Assert.Throws<DomainException>(() => ret.RemoveItem(item.Id));
        Assert.Equal("No se pueden quitar ítems de una devolución ya completada.", exception.Message);
    }

    [Fact]
    public void Complete_WithNoItems_ThrowsDomainException()
    {
        // Arrange
        var ret = new Return("Defecto", RefundMethod.Cash, "user-123", Guid.NewGuid());

        // Act & Assert
        var exception = Assert.Throws<DomainException>(() => ret.Complete());
        Assert.Equal("No se puede completar una devolución sin ítems.", exception.Message);
    }

    [Fact]
    public void Complete_AlreadyCompleted_ThrowsDomainException()
    {
        // Arrange
        var ret = new Return("Defecto", RefundMethod.Cash, "user-123", Guid.NewGuid());
        var prod = CreateProduct("Refresco", 20m);
        ret.AddItem(new ReturnItem(prod, 1m, 20m, 16m));
        ret.Complete();

        // Act & Assert
        var exception = Assert.Throws<DomainException>(() => ret.Complete());
        Assert.Equal("La devolución ya fue completada.", exception.Message);
    }

    [Fact]
    public void RecalculateTotals_CalculatesSubtotalTaxesAndRefundAmount()
    {
        // Arrange
        var ret = new Return("Defecto", RefundMethod.Cash, "user-123", Guid.NewGuid());
        var prodTaxable = CreateProduct("Refresco", 20m);
        var prodExempt = CreateProduct("Pan", 10m);

        var itemTaxable = new ReturnItem(prodTaxable, 3m, 20m, 16m, isTaxExempt: false); // 60 + 9.60 = 69.60
        var itemExempt = new ReturnItem(prodExempt, 2m, 10m, 0m, isTaxExempt: true);      // 20 + 0 = 20

        // Act
        ret.AddItem(itemTaxable);
        ret.AddItem(itemExempt);

        // Assert
        Assert.Equal(80m, ret.Subtotal);
        Assert.Equal(9.60m, ret.TotalTax);
        Assert.Equal(89.60m, ret.TotalRefund);
        Assert.Equal(2, ret.Taxes.Count);

        var tax16 = ret.Taxes.FirstOrDefault(t => t.Rate == 16m);
        Assert.NotNull(tax16);
        Assert.Equal(60m, tax16!.BaseAmount);
        Assert.Equal(9.60m, tax16.TaxAmount);

        var taxExempt = ret.Taxes.FirstOrDefault(t => t.IsExempt);
        Assert.NotNull(taxExempt);
        Assert.Equal(20m, taxExempt!.BaseAmount);
        Assert.Equal(0m, taxExempt.TaxAmount);
    }
}
