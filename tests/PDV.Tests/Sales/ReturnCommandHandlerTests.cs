using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using PDV.Application.Features.Sales.Commands.ReturnItem;
using PDV.Application.Features.Sales.Commands.ReturnSale;
using PDV.Domain.Entities;
using PDV.Domain.Enums;
using PDV.Domain.ValueObjects;
using PDV.Infrastructure.Persistence;
using PDV.Infrastructure.Persistence.Interceptors;
using PDV.Infrastructure.Repositories;
using Xunit;

namespace PDV.Tests.Sales;

public class ReturnCommandHandlerTests : IAsyncDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<AppDbContext> _options;

    public ReturnCommandHandlerTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        _options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .ConfigureWarnings(x => x.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .AddInterceptors(new DomainEventsInterceptor())
            .Options;
    }

    private async Task<AppDbContext> GetDbContextAsync()
    {
        var context = new AppDbContext(_options);
        await context.Database.EnsureCreatedAsync();
        return context;
    }

    [Fact]
    public async Task Handle_ReturnSaleCommand_SetsBranchIdAndSavesSuccessfully()
    {
        // Arrange
        await using var context = await GetDbContextAsync();

        // 1. Setup Product
        var product = new Product(
            name: "Test Product",
            code: "TP-100",
            price: 15m,
            saleType: SaleType.Piece,
            taxRate: TaxRateType.Rate16,
            category: "General"
        );
        context.Products.Add(product);

        // 2. Setup Branch, Stock, CashRegister and Shift
        var address = Address.Create("Calle Falsa 123", "Centro", "CDMX", "06000", "México");
        var branch = new Branch("Sucursal Centro", "SC001", address, "5551234567");
        context.Branches.Add(branch);

        var branchStock = new ProductBranchStock(product.Id, branch.Id, 8m, 0m); // 10m initial - 2m sold = 8m
        context.ProductBranchStocks.Add(branchStock);

        var cashRegister = new CashRegister("Caja 1", "CR01", branch.Id);
        context.CashRegisters.Add(cashRegister);

        var shift = new Shift(cashRegister.Id, "test-user", 1000m);
        context.Shifts.Add(shift);

        await context.SaveChangesAsync(CancellationToken.None);

        // 3. Create active paid Sale
        var sale = new Sale(
            saleNumber: SaleNumber.Generate(),
            paymentMethod: PaymentMethodType.Cash,
            userId: "test-user",
            shiftId: shift.Id,
            series: "V",
            folio: 1,
            clientId: null,
            cashRegisterId: cashRegister.Id
        );
        sale.SetBranch(branch.Id);

        var saleItem = new SaleItem(product, 2m, 16m, false, null);
        sale.AddItem(saleItem);
        sale.MarkAsPaid();

        context.Sales.Add(sale);
        await context.SaveChangesAsync(CancellationToken.None);

        var saleRepository = new SaleRepository(context);
        var handler = new ReturnSaleCommandHandler(saleRepository, context);

        var command = new ReturnSaleCommand(
            SaleId: sale.Id,
            Reason: "Cliente no lo quiso",
            CashierUserId: "test-user"
        );

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result);

        // Check return was saved with correct branch ID
        var savedReturn = await context.Returns
            .Include(r => r.Items)
            .FirstOrDefaultAsync(r => r.SaleId == sale.Id, CancellationToken.None);
        
        Assert.NotNull(savedReturn);
        Assert.Equal(branch.Id, savedReturn!.BranchId);
        Assert.NotEqual(Guid.Empty, savedReturn.BranchId);
        Assert.Single(savedReturn.Items);
        Assert.True(savedReturn.IsCompleted);

        // Check sale is marked as cancelled
        var updatedSale = await context.Sales.FindAsync(new object[] { sale.Id }, CancellationToken.None);
        Assert.NotNull(updatedSale);
        Assert.True(updatedSale!.IsCancelled);

        // Check stock is restored
        var updatedStock = await context.ProductBranchStocks
            .FirstOrDefaultAsync(s => s.ProductId == product.Id && s.BranchId == branch.Id, CancellationToken.None);
        Assert.NotNull(updatedStock);
        Assert.Equal(10m, updatedStock!.Stock);
    }

    [Fact]
    public async Task Handle_ReturnItemCommand_SetsBranchIdAndSavesSuccessfully()
    {
        // Arrange
        await using var context = await GetDbContextAsync();

        // 1. Setup Product
        var product = new Product(
            name: "Test Product 2",
            code: "TP-200",
            price: 25m,
            saleType: SaleType.Piece,
            taxRate: TaxRateType.Rate16,
            category: "General"
        );
        context.Products.Add(product);

        // 2. Setup Branch, Stock, CashRegister and Shift
        var address = Address.Create("Calle Falsa 123", "Centro", "CDMX", "06000", "México");
        var branch = new Branch("Sucursal Centro", "SC001", address, "5551234567");
        context.Branches.Add(branch);

        var branchStock = new ProductBranchStock(product.Id, branch.Id, 7m, 0m); // 10m initial - 3m sold = 7m
        context.ProductBranchStocks.Add(branchStock);

        var cashRegister = new CashRegister("Caja 1", "CR01", branch.Id);
        context.CashRegisters.Add(cashRegister);

        var shift = new Shift(cashRegister.Id, "test-user", 1000m);
        context.Shifts.Add(shift);

        await context.SaveChangesAsync(CancellationToken.None);

        // 3. Create active paid Sale
        var sale = new Sale(
            saleNumber: SaleNumber.Generate(),
            paymentMethod: PaymentMethodType.Cash,
            userId: "test-user",
            shiftId: shift.Id,
            series: "V",
            folio: 2,
            clientId: null,
            cashRegisterId: cashRegister.Id
        );
        sale.SetBranch(branch.Id);

        var saleItem = new SaleItem(product, 3m, 16m, false, null);
        sale.AddItem(saleItem);
        sale.MarkAsPaid();

        context.Sales.Add(sale);
        await context.SaveChangesAsync(CancellationToken.None);

        var handler = new ReturnItemCommandHandler(context);

        var command = new ReturnItemCommand(
            SaleItemId: saleItem.Id,
            Quantity: 2m,
            Reason: "Defecto en empaque",
            CashierUserId: "test-user"
        );

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result);

        // Check return was saved with correct branch ID
        var savedReturn = await context.Returns
            .Include(r => r.Items)
            .FirstOrDefaultAsync(r => r.SaleId == sale.Id, CancellationToken.None);
        
        Assert.NotNull(savedReturn);
        Assert.Equal(branch.Id, savedReturn!.BranchId);
        Assert.NotEqual(Guid.Empty, savedReturn.BranchId);
        Assert.Single(savedReturn.Items);
        Assert.Equal(2m, savedReturn.Items.First().Quantity);
        Assert.True(savedReturn.IsCompleted);

        // Check stock is restored partially
        var updatedStock = await context.ProductBranchStocks
            .FirstOrDefaultAsync(s => s.ProductId == product.Id && s.BranchId == branch.Id, CancellationToken.None);
        Assert.NotNull(updatedStock);
        Assert.Equal(9m, updatedStock!.Stock); // 10 initial - 3 sold + 2 returned = 9
    }

    public async ValueTask DisposeAsync()
    {
        await _connection.DisposeAsync();
    }
}
