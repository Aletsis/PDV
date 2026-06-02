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

        // 1. Crear producto con stock inicial usando constructor de dominio
        var product = new Product(
            name: "Test Product",
            code: "TP-001",
            price: 10m,
            stock: 10,
            saleType: SaleType.Piece,
            taxRate: TaxRateType.Rate16,
            category: "General"
        );
        context.Products.Add(product);

        // 2. Crear sucursal, caja registradora y turno activo necesarios para registrar la venta
        var address = Address.Create("Calle Falsa 123", "Centro", "CDMX", "06000", "México");
        var branch = new Branch("Sucursal Centro", "SC001", address, "5551234567");
        context.Branches.Add(branch);

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

        // Recargar producto y validar reducción de stock
        var updatedProduct = await context.Products.FindAsync(new object[] { product.Id }, CancellationToken.None);
        Assert.NotNull(updatedProduct);
        Assert.Equal(8, updatedProduct!.Stock);

        // Validar que la venta y sus ítems existan
        var sale = await context.Sales.Include(s => s.Items).FirstOrDefaultAsync(s => s.Id == saleId, CancellationToken.None);
        Assert.NotNull(sale);
        Assert.Single(sale!.Items);
    }
}
