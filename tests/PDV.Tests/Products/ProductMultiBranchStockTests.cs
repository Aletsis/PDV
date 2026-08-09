using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Moq;
using PDV.Application.Common.Interfaces;
using PDV.Application.Features.Products.Commands.CreateProduct;
using PDV.Application.Features.Products.Commands.UpdateProduct;
using PDV.Application.Features.Products.Queries.GetProductBranchStocks;
using PDV.Application.Features.Products.Queries.GetProducts;
using PDV.Domain.Entities;
using PDV.Domain.Enums;
using PDV.Infrastructure.Persistence;
using PDV.Infrastructure.Persistence.Interceptors;
using Xunit;

namespace PDV.Tests.Products;

public class ProductMultiBranchStockTests
{
    private DbContextOptions<AppDbContext> CreateNewContextOptions()
    {
        return new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: $"PDV_ProductMultiBranchStock_Test_{Guid.NewGuid()}")
            .ConfigureWarnings(x => x.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .AddInterceptors(new DomainEventsInterceptor())
            .Options;
    }

    [Fact]
    public async Task CreateProduct_WithInitialStock_CreatesProductBranchStockAndInitialInventoryMovement()
    {
        // Arrange
        var options = CreateNewContextOptions();
        await using var context = new AppDbContext(options);

        var branch1 = new Branch("Sucursal Centro", "SC001", null, "5551112222", isMainBranch: true);
        var branch2 = new Branch("Sucursal Norte", "SN001", null, "5553334444");
        context.Branches.AddRange(branch1, branch2);
        await context.SaveChangesAsync(CancellationToken.None);

        var syncMock = new Mock<IComercialApiSyncService>();
        var handler = new CreateProductCommandHandler(context, syncMock.Object);

        var command = new CreateProductCommand
        {
            Name = "Aceite Nutrioli 1L",
            Code = "ACE-NUT-1L",
            Price = 45m,
            Cost = 35m,
            Stock = 20m,
            MinStock = 5m,
            BranchId = branch1.Id,
            SaleType = "Piece",
            TaxRate = "Rate16"
        };

        // Act
        var productId = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.NotEqual(Guid.Empty, productId);

        var product = await context.Products.FindAsync(productId);
        Assert.NotNull(product);
        Assert.Equal("Aceite Nutrioli 1L", product!.Name);

        // Verify Branch Stocks
        var branchStocks = await context.ProductBranchStocks
            .Where(s => s.ProductId == productId)
            .ToListAsync();
        Assert.Equal(2, branchStocks.Count);

        var stockBranch1 = branchStocks.First(s => s.BranchId == branch1.Id);
        Assert.Equal(20m, stockBranch1.Stock);
        Assert.Equal(5m, stockBranch1.MinStock);

        var stockBranch2 = branchStocks.First(s => s.BranchId == branch2.Id);
        Assert.Equal(0m, stockBranch2.Stock);
        Assert.Equal(5m, stockBranch2.MinStock);

        // Verify InitialInventory movement in InventoryMovements
        var movements = await context.InventoryMovements
            .Where(m => m.ProductId == productId)
            .ToListAsync();
        Assert.Single(movements);

        var initialMovement = movements.First();
        Assert.Equal(branch1.Id, initialMovement.BranchId);
        Assert.Equal(20m, initialMovement.Quantity);
        Assert.Equal(InventoryMovementType.InitialInventory, initialMovement.Type);
        Assert.Equal("Inventario inicial", initialMovement.Remarks);
    }

    [Fact]
    public async Task CreateProduct_WithZeroStock_DoesNotCreateInventoryMovement()
    {
        // Arrange
        var options = CreateNewContextOptions();
        await using var context = new AppDbContext(options);

        var branch = new Branch("Sucursal Centro", "SC001", null, "5551112222", isMainBranch: true);
        context.Branches.Add(branch);
        await context.SaveChangesAsync(CancellationToken.None);

        var syncMock = new Mock<IComercialApiSyncService>();
        var handler = new CreateProductCommandHandler(context, syncMock.Object);

        var command = new CreateProductCommand
        {
            Name = "Atún Dolores en Agua",
            Code = "ATUN-DOL",
            Price = 22m,
            Cost = 16m,
            Stock = 0m,
            MinStock = 10m,
            BranchId = branch.Id
        };

        // Act
        var productId = await handler.Handle(command, CancellationToken.None);

        // Assert
        var stock = await context.ProductBranchStocks
            .FirstOrDefaultAsync(s => s.ProductId == productId && s.BranchId == branch.Id);
        Assert.NotNull(stock);
        Assert.Equal(0m, stock!.Stock);

        var movements = await context.InventoryMovements
            .Where(m => m.ProductId == productId)
            .ToListAsync();
        Assert.Empty(movements);
    }

    [Fact]
    public async Task UpdateProduct_DoesNotModifyStockDirectly()
    {
        // Arrange
        var options = CreateNewContextOptions();
        await using var context = new AppDbContext(options);

        var branch = new Branch("Sucursal Centro", "SC001", null, "5551112222", isMainBranch: true);
        context.Branches.Add(branch);

        var product = new Product("Galletas Marias", "GAL-MAR", 18m, SaleType.Piece, TaxRateType.Rate16, "Abarrotes");
        context.Products.Add(product);

        var branchStock = new ProductBranchStock(product.Id, branch.Id, 50m, 5m);
        context.ProductBranchStocks.Add(branchStock);

        await context.SaveChangesAsync(CancellationToken.None);

        var syncMock = new Mock<IComercialApiSyncService>();
        var handler = new UpdateProductCommandHandler(context, syncMock.Object);

        var updateCommand = new UpdateProductCommand
        {
            Id = product.Id,
            Name = "Galletas Marias Gamesa 170g",
            Code = "GAL-MAR",
            Price = 20m,
            Cost = 14m,
            Stock = 999m, // Attempting to change stock directly via basic edit
            MinStock = 8m,
            BranchId = branch.Id
        };

        // Act
        await handler.Handle(updateCommand, CancellationToken.None);

        // Assert
        var updatedProduct = await context.Products.FindAsync(product.Id);
        Assert.NotNull(updatedProduct);
        Assert.Equal("Galletas Marias Gamesa 170g", updatedProduct!.Name);
        Assert.Equal(20m, updatedProduct.Price);

        var updatedStock = await context.ProductBranchStocks
            .FirstOrDefaultAsync(s => s.ProductId == product.Id && s.BranchId == branch.Id);
        Assert.NotNull(updatedStock);
        // Stock must remain intact at 50, not modified to 999
        Assert.Equal(50m, updatedStock!.Stock);
        // MinStock is updated
        Assert.Equal(8m, updatedStock.MinStock);
    }

    [Fact]
    public async Task GetProductBranchStocksQuery_ReturnsStockForAllBranches()
    {
        // Arrange
        var options = CreateNewContextOptions();
        await using var context = new AppDbContext(options);

        var branch1 = new Branch("Sucursal Norte", "SN001", null, "5551112222");
        var branch2 = new Branch("Sucursal Sur", "SS001", null, "5553334444");
        context.Branches.AddRange(branch1, branch2);

        var product = new Product("Leche Entera 1L", "LEC-ENT-1L", 28m, SaleType.Piece, TaxRateType.Rate16, "Lácteos");
        context.Products.Add(product);

        var stock1 = new ProductBranchStock(product.Id, branch1.Id, 15m, 3m);
        var stock2 = new ProductBranchStock(product.Id, branch2.Id, 8m, 5m);
        context.ProductBranchStocks.AddRange(stock1, stock2);

        await context.SaveChangesAsync(CancellationToken.None);

        var handler = new GetProductBranchStocksQueryHandler(context);

        // Act
        var result = await handler.Handle(new GetProductBranchStocksQuery(product.Id), CancellationToken.None);

        // Assert
        Assert.Equal(2, result.Count);

        var r1 = result.First(r => r.BranchId == branch1.Id);
        Assert.Equal("Sucursal Norte", r1.BranchName);
        Assert.Equal(15m, r1.Stock);
        Assert.Equal(3m, r1.MinStock);
        Assert.False(r1.IsLowStock);
        Assert.False(r1.IsOutOfStock);

        var r2 = result.First(r => r.BranchId == branch2.Id);
        Assert.Equal("Sucursal Sur", r2.BranchName);
        Assert.Equal(8m, r2.Stock);
        Assert.Equal(5m, r2.MinStock);
    }

    [Fact]
    public async Task GetProductsQuery_WithBranchFilter_ReturnsBranchSpecificStock()
    {
        // Arrange
        var options = CreateNewContextOptions();
        await using var context = new AppDbContext(options);

        var branch1 = new Branch("Sucursal Norte", "SN001", null, "5551112222");
        var branch2 = new Branch("Sucursal Sur", "SS001", null, "5553334444");
        context.Branches.AddRange(branch1, branch2);

        var product = new Product("Arroz 1kg", "ARR-1KG", 25m, SaleType.Piece, TaxRateType.Rate16, "Granos");
        context.Products.Add(product);

        var stock1 = new ProductBranchStock(product.Id, branch1.Id, 30m, 5m);
        var stock2 = new ProductBranchStock(product.Id, branch2.Id, 10m, 5m);
        context.ProductBranchStocks.AddRange(stock1, stock2);

        await context.SaveChangesAsync(CancellationToken.None);

        var handler = new GetProductsQueryHandler(context);

        // Act - query for branch1
        var resultBranch1 = await handler.Handle(new GetProductsQuery(BranchId: branch1.Id), CancellationToken.None);
        var pBranch1 = resultBranch1.First(p => p.Id == product.Id);
        Assert.Equal(30m, pBranch1.Stock);

        // Act - query for all branches (BranchId = null)
        var resultAll = await handler.Handle(new GetProductsQuery(BranchId: null), CancellationToken.None);
        var pAll = resultAll.First(p => p.Id == product.Id);
        Assert.Equal(40m, pAll.Stock);
    }
}
