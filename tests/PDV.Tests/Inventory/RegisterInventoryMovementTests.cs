using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using PDV.Application.Features.InventoryMovements.Commands.RegisterInventoryMovement;
using PDV.Domain.Entities;
using PDV.Domain.Enums;
using PDV.Domain.Exceptions;
using PDV.Infrastructure.Persistence;
using PDV.Infrastructure.Persistence.Interceptors;
using Xunit;

namespace PDV.Tests.Inventory;

public class RegisterInventoryMovementTests
{
    private DbContextOptions<AppDbContext> CreateNewContextOptions()
    {
        return new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: $"PDV_RegisterInventoryMovement_Test_{Guid.NewGuid()}")
            .ConfigureWarnings(x => x.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .AddInterceptors(new DomainEventsInterceptor())
            .Options;
    }

    [Fact]
    public async Task RegisterInventoryMovement_Purchase_IncreasesStockAndRegistersMovement()
    {
        // Arrange
        var options = CreateNewContextOptions();
        await using var context = new AppDbContext(options);

        var product = new Product("Sabritas 40g", "SAB-40G", 15m, SaleType.Piece, TaxRateType.Rate16, "Botanas");
        context.Products.Add(product);

        var branch = new Branch("Sucursal Norte", "SN001", null, "5551112222");
        context.Branches.Add(branch);

        var branchStock = new ProductBranchStock(product.Id, branch.Id, 10m, 2m);
        context.ProductBranchStocks.Add(branchStock);

        await context.SaveChangesAsync(CancellationToken.None);

        var handler = new RegisterInventoryMovementCommandHandler(context);
        var command = new RegisterInventoryMovementCommand
        {
            BranchId = branch.Id,
            Type = InventoryMovementType.Purchase,
            Remarks = "Compra de mercancía",
            Items = new List<InventoryMovementItemCommand>
            {
                new() { ProductId = product.Id, Quantity = 5m }
            }
        };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result);

        var updatedStock = await context.ProductBranchStocks
            .FirstOrDefaultAsync(s => s.ProductId == product.Id && s.BranchId == branch.Id);
        Assert.NotNull(updatedStock);
        Assert.Equal(15m, updatedStock!.Stock);

        var movement = await context.InventoryMovements
            .FirstOrDefaultAsync(m => m.ProductId == product.Id && m.BranchId == branch.Id);
        Assert.NotNull(movement);
        Assert.Equal(5m, movement!.Quantity);
        Assert.Equal(InventoryMovementType.Purchase, movement.Type);
        Assert.Equal("Compra de mercancía", movement.Remarks);
    }

    [Fact]
    public async Task RegisterInventoryMovement_AdjustmentOutput_WithSufficientStock_DecreasesStock()
    {
        // Arrange
        var options = CreateNewContextOptions();
        await using var context = new AppDbContext(options);

        var product = new Product("Sabritas 40g", "SAB-40G", 15m, SaleType.Piece, TaxRateType.Rate16, "Botanas");
        context.Products.Add(product);

        var branch = new Branch("Sucursal Norte", "SN001", null, "5551112222");
        context.Branches.Add(branch);

        var branchStock = new ProductBranchStock(product.Id, branch.Id, 10m, 2m);
        context.ProductBranchStocks.Add(branchStock);

        await context.SaveChangesAsync(CancellationToken.None);

        var handler = new RegisterInventoryMovementCommandHandler(context);
        var command = new RegisterInventoryMovementCommand
        {
            BranchId = branch.Id,
            Type = InventoryMovementType.AdjustmentOutput,
            Remarks = "Merma de botanas dañadas",
            Items = new List<InventoryMovementItemCommand>
            {
                new() { ProductId = product.Id, Quantity = 3m }
            }
        };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result);

        var updatedStock = await context.ProductBranchStocks
            .FirstOrDefaultAsync(s => s.ProductId == product.Id && s.BranchId == branch.Id);
        Assert.NotNull(updatedStock);
        Assert.Equal(7m, updatedStock!.Stock);

        var movement = await context.InventoryMovements
            .FirstOrDefaultAsync(m => m.ProductId == product.Id && m.BranchId == branch.Id);
        Assert.NotNull(movement);
        Assert.Equal(-3m, movement!.Quantity);
        Assert.Equal(InventoryMovementType.AdjustmentOutput, movement.Type);
        Assert.Equal("Merma de botanas dañadas", movement.Remarks);
    }

    [Fact]
    public async Task RegisterInventoryMovement_AdjustmentOutput_WithInsufficientStock_ThrowsDomainException()
    {
        // Arrange
        var options = CreateNewContextOptions();
        await using var context = new AppDbContext(options);

        var product = new Product("Sabritas 40g", "SAB-40G", 15m, SaleType.Piece, TaxRateType.Rate16, "Botanas");
        context.Products.Add(product);

        var branch = new Branch("Sucursal Norte", "SN001", null, "5551112222");
        context.Branches.Add(branch);

        var branchStock = new ProductBranchStock(product.Id, branch.Id, 2m, 1m);
        context.ProductBranchStocks.Add(branchStock);

        await context.SaveChangesAsync(CancellationToken.None);

        var handler = new RegisterInventoryMovementCommandHandler(context);
        var command = new RegisterInventoryMovementCommand
        {
            BranchId = branch.Id,
            Type = InventoryMovementType.AdjustmentOutput,
            Remarks = "Merma grande",
            Items = new List<InventoryMovementItemCommand>
            {
                new() { ProductId = product.Id, Quantity = 5m }
            }
        };

        // Act & Assert
        await Assert.ThrowsAsync<DomainException>(() => handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task RegisterInventoryMovement_Transfer_DecreasesSourceAndIncreasesDestination()
    {
        // Arrange
        var options = CreateNewContextOptions();
        await using var context = new AppDbContext(options);

        var product = new Product("Sabritas 40g", "SAB-40G", 15m, SaleType.Piece, TaxRateType.Rate16, "Botanas");
        context.Products.Add(product);

        var sourceBranch = new Branch("Sucursal Centro", "SC001", null, "5552223333");
        var destBranch = new Branch("Sucursal Sur", "SS001", null, "5553334444");
        context.Branches.AddRange(sourceBranch, destBranch);

        var sourceStock = new ProductBranchStock(product.Id, sourceBranch.Id, 10m, 2m);
        var destStock = new ProductBranchStock(product.Id, destBranch.Id, 0m, 2m);
        context.ProductBranchStocks.AddRange(sourceStock, destStock);

        await context.SaveChangesAsync(CancellationToken.None);

        var handler = new RegisterInventoryMovementCommandHandler(context);
        var command = new RegisterInventoryMovementCommand
        {
            BranchId = sourceBranch.Id,
            DestinationBranchId = destBranch.Id,
            Type = InventoryMovementType.Transfer,
            Remarks = "Traspaso de excedente",
            Items = new List<InventoryMovementItemCommand>
            {
                new() { ProductId = product.Id, Quantity = 4m }
            }
        };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result);

        var updatedSourceStock = await context.ProductBranchStocks
            .FirstOrDefaultAsync(s => s.ProductId == product.Id && s.BranchId == sourceBranch.Id);
        Assert.NotNull(updatedSourceStock);
        Assert.Equal(6m, updatedSourceStock!.Stock);

        var updatedDestStock = await context.ProductBranchStocks
            .FirstOrDefaultAsync(s => s.ProductId == product.Id && s.BranchId == destBranch.Id);
        Assert.NotNull(updatedDestStock);
        Assert.Equal(4m, updatedDestStock!.Stock);

        var movements = await context.InventoryMovements
            .Where(m => m.ProductId == product.Id)
            .ToListAsync();
        Assert.Equal(2, movements.Count);

        var sourceMovement = movements.FirstOrDefault(m => m.BranchId == sourceBranch.Id);
        Assert.NotNull(sourceMovement);
        Assert.Equal(-4m, sourceMovement!.Quantity);
        Assert.Equal(InventoryMovementType.Transfer, sourceMovement.Type);
        Assert.Contains("Traspaso (Salida)", sourceMovement.Remarks);

        var destMovement = movements.FirstOrDefault(m => m.BranchId == destBranch.Id);
        Assert.NotNull(destMovement);
        Assert.Equal(4m, destMovement!.Quantity);
        Assert.Equal(InventoryMovementType.Transfer, destMovement.Type);
        Assert.Contains("Traspaso (Entrada)", destMovement.Remarks);

        Assert.Equal(sourceMovement.ReferenceId, destMovement.ReferenceId);
        Assert.NotNull(sourceMovement.ReferenceId);
    }

    [Fact]
    public async Task RegisterInventoryMovement_MultipleItems_AppliesStockChangesAndRegistersMovements()
    {
        // Arrange
        var options = CreateNewContextOptions();
        await using var context = new AppDbContext(options);

        var product1 = new Product("Sabritas 40g", "SAB-40G", 15m, SaleType.Piece, TaxRateType.Rate16, "Botanas");
        var product2 = new Product("Coca Cola 600ml", "COCA-600", 18m, SaleType.Piece, TaxRateType.Rate16, "Refrescos");
        context.Products.AddRange(product1, product2);

        var branch = new Branch("Sucursal Norte", "SN001", null, "5551112222");
        context.Branches.Add(branch);

        var stock1 = new ProductBranchStock(product1.Id, branch.Id, 10m, 2m);
        var stock2 = new ProductBranchStock(product2.Id, branch.Id, 20m, 5m);
        context.ProductBranchStocks.AddRange(stock1, stock2);

        await context.SaveChangesAsync(CancellationToken.None);

        var handler = new RegisterInventoryMovementCommandHandler(context);
        var command = new RegisterInventoryMovementCommand
        {
            BranchId = branch.Id,
            Type = InventoryMovementType.Purchase,
            Remarks = "Compra lote mixto",
            Items = new List<InventoryMovementItemCommand>
            {
                new() { ProductId = product1.Id, Quantity = 5m },
                new() { ProductId = product2.Id, Quantity = 10m }
            }
        };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result);

        var updatedStock1 = await context.ProductBranchStocks.FirstAsync(s => s.ProductId == product1.Id && s.BranchId == branch.Id);
        var updatedStock2 = await context.ProductBranchStocks.FirstAsync(s => s.ProductId == product2.Id && s.BranchId == branch.Id);
        Assert.Equal(15m, updatedStock1.Stock);
        Assert.Equal(30m, updatedStock2.Stock);

        var movements = await context.InventoryMovements.Where(m => m.BranchId == branch.Id).ToListAsync();
        Assert.Equal(2, movements.Count);

        var mov1 = movements.First(m => m.ProductId == product1.Id);
        var mov2 = movements.First(m => m.ProductId == product2.Id);
        Assert.Equal(5m, mov1.Quantity);
        Assert.Equal(10m, mov2.Quantity);
        Assert.Equal(InventoryMovementType.Purchase, mov1.Type);
        Assert.Equal(InventoryMovementType.Purchase, mov2.Type);

        // Deberían compartir el mismo ReferenceId (batchReferenceId)
        Assert.NotNull(mov1.ReferenceId);
        Assert.Equal(mov1.ReferenceId, mov2.ReferenceId);
    }

    [Fact]
    public async Task RegisterInventoryMovement_InLocalMode_ThrowsDomainException()
    {
        // Arrange
        var connection = new Microsoft.Data.Sqlite.SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .AddInterceptors(new DomainEventsInterceptor())
            .Options;

        await using (var setupContext = new AppDbContext(options))
        {
            await setupContext.Database.EnsureCreatedAsync();

            var product = new Product("Sabritas 40g", "SAB-40G", 15m, SaleType.Piece, TaxRateType.Rate16, "Botanas");
            setupContext.Products.Add(product);

            var branch = new Branch("Sucursal Norte", "SN001", null, "5551112222");
            setupContext.Branches.Add(branch);

            var branchStock = new ProductBranchStock(product.Id, branch.Id, 10m, 2m);
            setupContext.ProductBranchStocks.Add(branchStock);

            await setupContext.SaveChangesAsync(CancellationToken.None);
        }

        await using var context = new AppDbContext(options);
        var productInDb = await context.Products.FirstAsync();
        var branchInDb = await context.Branches.FirstAsync();

        var handler = new RegisterInventoryMovementCommandHandler(context);
        var command = new RegisterInventoryMovementCommand
        {
            BranchId = branchInDb.Id,
            Type = InventoryMovementType.Purchase,
            Remarks = "Intento de compra en modo local",
            Items = new List<InventoryMovementItemCommand>
            {
                new() { ProductId = productInDb.Id, Quantity = 5m }
            }
        };

        // Act & Assert
        var ex = await Assert.ThrowsAsync<DomainException>(() => handler.Handle(command, CancellationToken.None));
        Assert.Contains("no están permitidas en modo local", ex.Message);
        
        await connection.CloseAsync();
    }
}
