using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using PDV.Application.Features.Sales.Commands.CreateSale;
using PDV.Application.Features.Sales.Dtos;
using PDV.Domain.Entities;
using PDV.Domain.Enums;
using PDV.Domain.Events;
using PDV.Infrastructure.Persistence;
using PDV.Infrastructure.Persistence.Interceptors;
using PDV.Infrastructure.Repositories;
using Xunit;

namespace PDV.Tests.Inventory;

public class InventoryTests
{
    private DbContextOptions<AppDbContext> CreateNewContextOptions()
    {
        return new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: $"PDV_Inventory_Test_{Guid.NewGuid()}")
            .ConfigureWarnings(x => x.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .AddInterceptors(new DomainEventsInterceptor())
            .Options;
    }

    [Fact]
    public void Product_ApplyMovement_UpdatesStockAndTracksMovements()
    {
        // Arrange
        var product = new Product(
            name: "Coca Cola 600ml",
            code: "CC-600",
            price: 18.50m,
            stock: 10,
            saleType: SaleType.Piece,
            taxRate: TaxRateType.Rate16,
            category: "Refrescos"
        );

        // Act - Simular venta de 3 piezas
        product.ApplyMovement(-3m, InventoryMovementType.Sale, Guid.NewGuid(), "Venta POS");

        // Assert
        Assert.Equal(7m, product.Stock);

        // La colección Movements es de solo lectura desde BD; el movimiento
        // se verifica a través del evento de dominio levantado
        var domainEvent = product.DomainEvents
            .OfType<InventoryMovementRegisteredEvent>()
            .FirstOrDefault();
        Assert.NotNull(domainEvent);
        Assert.Equal(-3m, domainEvent!.Quantity);
        Assert.Equal(InventoryMovementType.Sale, domainEvent.Type);
        Assert.Equal("Venta POS", domainEvent.Remarks);
    }

    [Fact]
    public async Task CreateSale_RegistersInventoryMovementInDatabaseAndOutbox()
    {
        // Arrange
        var options = CreateNewContextOptions();
        await using var context = new AppDbContext(options);

        var product = new Product(
            name: "Sabritas Sal 40g",
            code: "SAB-SAL",
            price: 15m,
            stock: 100,
            saleType: SaleType.Piece,
            taxRate: TaxRateType.Rate16,
            category: "Botanas"
        );
        context.Products.Add(product);

        var branch = new Branch("Sucursal Centro", "SC001", null, "5551234567");
        context.Branches.Add(branch);

        var cashRegister = new CashRegister("Caja 1", "CR01", branch.Id);
        context.CashRegisters.Add(cashRegister);

        var shift = new Shift(cashRegister.Id, "admin", 500m);
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
                new CartItemDto { Product = product, Quantity = 5 }
            },
            UserId = "admin",
            CashRegisterId = cashRegister.Id,
            PaymentMethod = "Cash",
            IsPaid = true
        };

        // Act
        var saleId = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.NotEqual(Guid.Empty, saleId);

        // Validar que el stock del producto disminuyó
        var updatedProduct = await context.Products.FindAsync(new object[] { product.Id }, CancellationToken.None);
        Assert.NotNull(updatedProduct);
        Assert.Equal(95m, updatedProduct!.Stock);

        // Validar que se guardó el movimiento transaccional de inventario en DB
        var movement = await context.InventoryMovements
            .FirstOrDefaultAsync(m => m.ProductId == product.Id && m.ReferenceId == saleId, CancellationToken.None);
        Assert.NotNull(movement);
        Assert.Equal(-5m, movement!.Quantity);
        Assert.Equal(InventoryMovementType.Sale, movement.Type);

        // Validar que se generó un OutboxMessage con el evento de movimiento para la sincronización
        var outboxMessage = await context.OutboxMessages
            .FirstOrDefaultAsync(o => o.EventType == "InventoryMovementRegisteredEvent", CancellationToken.None);
        Assert.NotNull(outboxMessage);
        Assert.Contains(movement.Id.ToString(), outboxMessage!.Payload);
    }
}
