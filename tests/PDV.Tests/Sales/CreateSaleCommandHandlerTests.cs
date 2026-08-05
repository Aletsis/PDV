using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using PDV.Application.Features.Sales.Commands.CreateSale;
using PDV.Application.Features.Sales.Dtos;
using PDV.Domain.Entities;
using PDV.Domain.Enums;
using PDV.Domain.ValueObjects;
using PDV.Infrastructure.Persistence;
using PDV.Infrastructure.Persistence.Interceptors;
using PDV.Infrastructure.Repositories;
using Xunit;

namespace PDV.Tests.Sales;

public class CreateSaleCommandHandlerTests
{
    [Fact]
    public async Task Handle_CreatesSaleAndReducesStock()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: $"PDV_Sales_Test_{Guid.NewGuid()}")
            .ConfigureWarnings(x => x.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .AddInterceptors(new DomainEventsInterceptor())
            .Options;

        await using var context = new AppDbContext(options);

        // 1. Crear producto usando constructor de dominio (sin parámetros obsoletos de stock)
        var product = new Product(
            name: "Test Product",
            code: "TP-001",
            price: 10m,
            saleType: SaleType.Piece,
            taxRate: TaxRateType.Rate16,
            category: "General"
        );
        context.Products.Add(product);

        // 2. Crear sucursal, caja registradora y turno activo necesarios para registrar la venta
        var address = Address.Create("Calle Falsa 123", "Centro", "CDMX", "06000", "México");
        var branch = new Branch("Sucursal Centro", "SC001", address, "5551234567");
        context.Branches.Add(branch);

        // Registrar stock por sucursal
        var branchStock = new ProductBranchStock(product.Id, branch.Id, 10m, 0m);
        context.ProductBranchStocks.Add(branchStock);

        var cashRegister = new CashRegister("Caja 1", "CR01", branch.Id);
        context.CashRegisters.Add(cashRegister);

        var shift = new Shift(cashRegister.Id, "test-user", 1000m);
        context.Shifts.Add(shift);

        await context.SaveChangesAsync(CancellationToken.None);

        var productRepository = new ProductRepository(context);
        var saleRepository = new SaleRepository(context);
        var ticketSequenceRepository = new TicketSequenceRepository(context);

        var handler = new CreateSaleCommandHandler(saleRepository, productRepository, ticketSequenceRepository, context);

        var command = new CreateSaleCommand
        {
            Items = new List<CartItemDto>
            {
                new CartItemDto 
                { 
                    Product = product, 
                    Quantity = 2 
                }
            },
            UserId = "test-user",
            CashRegisterId = cashRegister.Id,
            PaymentMethod = "Cash",
            IsPaid = true
        };

        // Act
        var saleId = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.NotEqual(Guid.Empty, saleId);

        // Recargar stock y validar reducción de stock en la sucursal
        var updatedStock = await context.ProductBranchStocks
            .FirstOrDefaultAsync(s => s.ProductId == product.Id && s.BranchId == branch.Id, CancellationToken.None);
        Assert.NotNull(updatedStock);
        Assert.Equal(8m, updatedStock!.Stock);

        // Validar que la venta y sus ítems existan
        var sale = await context.Sales.Include(s => s.Items).FirstOrDefaultAsync(s => s.Id == saleId, CancellationToken.None);
        Assert.NotNull(sale);
        Assert.Single(sale!.Items);
    }

    [Fact]
    public async Task Handle_WithSinControlProduct_DoesNotValidateStockOrReduceIt()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: $"PDV_Sales_Test_{Guid.NewGuid()}")
            .ConfigureWarnings(x => x.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .AddInterceptors(new DomainEventsInterceptor())
            .Options;

        await using var context = new AppDbContext(options);

        // Create product with ControlExistencia = ControlExistencia.SinControl
        var product = new Product(
            name: "Service Item",
            code: "SERV-001",
            price: 50m,
            saleType: SaleType.Piece,
            taxRate: TaxRateType.Rate16,
            category: "Services",
            cost: 0,
            plu: null,
            barcode: null,
            description: "An installation service",
            branchId: null,
            wholesalePrice: null,
            wholesaleMinQuantity: null,
            satCode: "",
            type: ProductType.Servicio,
            controlExistencia: ControlExistencia.SinControl
        );
        context.Products.Add(product);

        var address = Address.Create("Calle Falsa 123", "Centro", "CDMX", "06000", "México");
        var branch = new Branch("Sucursal Centro", "SC001", address, "5551234567");
        context.Branches.Add(branch);

        // Registrar stock por sucursal con 0 stock inicial
        var branchStock = new ProductBranchStock(product.Id, branch.Id, 0m, 0m);
        context.ProductBranchStocks.Add(branchStock);

        var cashRegister = new CashRegister("Caja 1", "CR01", branch.Id);
        context.CashRegisters.Add(cashRegister);

        var shift = new Shift(cashRegister.Id, "test-user", 1000m);
        context.Shifts.Add(shift);

        await context.SaveChangesAsync(CancellationToken.None);

        var productRepository = new ProductRepository(context);
        var saleRepository = new SaleRepository(context);
        var ticketSequenceRepository = new TicketSequenceRepository(context);

        var handler = new CreateSaleCommandHandler(saleRepository, productRepository, ticketSequenceRepository, context);

        var command = new CreateSaleCommand
        {
            Items = new List<CartItemDto>
            {
                new CartItemDto 
                { 
                    Product = product, 
                    Quantity = 1 
                }
            },
            UserId = "test-user",
            CashRegisterId = cashRegister.Id,
            PaymentMethod = "Cash",
            IsPaid = true
        };

        // Act
        var saleId = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.NotEqual(Guid.Empty, saleId);

        // Verify stock remains 0 (since it is SinControl, it should not change)
        var updatedStock = await context.ProductBranchStocks
            .FirstOrDefaultAsync(s => s.ProductId == product.Id && s.BranchId == branch.Id, CancellationToken.None);
        Assert.NotNull(updatedStock);
        Assert.Equal(0m, updatedStock!.Stock);

        // Verify that no InventoryMovement was created for this product
        var hasMovements = await context.InventoryMovements
            .AnyAsync(m => m.ProductId == product.Id, CancellationToken.None);
        Assert.False(hasMovements);

        // Verify sale items
        var sale = await context.Sales.Include(s => s.Items).FirstOrDefaultAsync(s => s.Id == saleId, CancellationToken.None);
        Assert.NotNull(sale);
        Assert.Single(sale!.Items);
    }
}
