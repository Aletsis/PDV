using System;
using System.Linq;
using PDV.Domain.Entities;
using PDV.Domain.Enums;
using PDV.Domain.Events;
using PDV.Domain.Exceptions;
using Xunit;

namespace PDV.Tests.Domain.Products;

public class ProductBranchStockTests
{
    [Fact]
    public void Constructor_WithValidParameters_InitializesCorrectly()
    {
        // Arrange
        var productId = Guid.NewGuid();
        var branchId = Guid.NewGuid();

        // Act
        var branchStock = new ProductBranchStock(productId, branchId, 10m, 2m);

        // Assert
        Assert.Equal(productId, branchStock.ProductId);
        Assert.Equal(branchId, branchStock.BranchId);
        Assert.Equal(10m, branchStock.Stock);
        Assert.Equal(2m, branchStock.MinStock);
    }

    [Theory]
    [InlineData("00000000-0000-0000-0000-000000000000", "00000000-0000-0000-0000-000000000001", 10, 2, "El ID de producto es requerido.")]
    [InlineData("00000000-0000-0000-0000-000000000001", "00000000-0000-0000-0000-000000000000", 10, 2, "El ID de sucursal es requerido.")]
    [InlineData("00000000-0000-0000-0000-000000000001", "00000000-0000-0000-0000-000000000002", -5, 2, "El stock inicial no puede ser negativo.")]
    [InlineData("00000000-0000-0000-0000-000000000001", "00000000-0000-0000-0000-000000000002", 10, -1, "El stock mínimo no puede ser negativo.")]
    public void Constructor_WithInvalidParameters_ThrowsDomainException(string productIdStr, string branchIdStr, decimal stock, decimal minStock, string expectedMessage)
    {
        // Arrange
        var productId = Guid.Parse(productIdStr);
        var branchId = Guid.Parse(branchIdStr);

        // Act & Assert
        var exception = Assert.Throws<DomainException>(() => new ProductBranchStock(productId, branchId, stock, minStock));
        Assert.Equal(expectedMessage, exception.Message);
    }

    [Fact]
    public void ReduceStock_WithValidQuantity_DecrementsStock()
    {
        // Arrange
        var branchStock = new ProductBranchStock(Guid.NewGuid(), Guid.NewGuid(), 10m);

        // Act
        branchStock.ReduceStock(3m);

        // Assert
        Assert.Equal(7m, branchStock.Stock);
    }

    [Fact]
    public void ReduceStock_WithInsufficientStock_ThrowsDomainException()
    {
        // Arrange
        var branchStock = new ProductBranchStock(Guid.NewGuid(), Guid.NewGuid(), 5m);

        // Act & Assert
        var exception = Assert.Throws<DomainException>(() => branchStock.ReduceStock(10m));
        Assert.Contains("Stock insuficiente", exception.Message);
    }

    [Fact]
    public void IncreaseStock_WithValidQuantity_IncrementsStock()
    {
        // Arrange
        var branchStock = new ProductBranchStock(Guid.NewGuid(), Guid.NewGuid(), 10m);

        // Act
        branchStock.IncreaseStock(5m);

        // Assert
        Assert.Equal(15m, branchStock.Stock);
    }

    [Fact]
    public void AdjustStock_WithValidValue_AdjustsStock()
    {
        // Arrange
        var branchStock = new ProductBranchStock(Guid.NewGuid(), Guid.NewGuid(), 10m);

        // Act
        branchStock.AdjustStock(12.5m);

        // Assert
        Assert.Equal(12.5m, branchStock.Stock);
    }

    [Fact]
    public void ApplyMovement_WithValidQuantity_UpdatesStockAndRaisesEvent()
    {
        // Arrange
        var productId = Guid.NewGuid();
        var branchId = Guid.NewGuid();
        var branchStock = new ProductBranchStock(productId, branchId, 10m);

        // Act
        branchStock.ApplyMovement(-3m, InventoryMovementType.Sale, remarks: "Venta");

        // Assert
        Assert.Equal(7m, branchStock.Stock);

        var movementEvent = branchStock.DomainEvents.OfType<InventoryMovementRegisteredEvent>().FirstOrDefault();
        Assert.NotNull(movementEvent);
        Assert.Equal(productId, movementEvent!.ProductId);
        Assert.Equal(branchId, movementEvent.BranchId);
        Assert.Equal(-3m, movementEvent.Quantity);
        Assert.Equal(InventoryMovementType.Sale, movementEvent.Type);
        Assert.Equal("Venta", movementEvent.Remarks);
    }

    [Fact]
    public void ApplyMovement_WithZeroQuantity_ThrowsDomainException()
    {
        // Arrange
        var branchStock = new ProductBranchStock(Guid.NewGuid(), Guid.NewGuid(), 10m);

        // Act & Assert
        var exception = Assert.Throws<DomainException>(() => branchStock.ApplyMovement(0m, InventoryMovementType.AdjustmentInput));
        Assert.Equal("La cantidad del movimiento no puede ser cero.", exception.Message);
    }
}
