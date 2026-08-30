using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using PDV.Application.Features.Orders.Queries.GetOrdersForFulfillment;
using PDV.Domain.Entities;
using PDV.Domain.Enums;
using PDV.Domain.ValueObjects;
using PDV.Infrastructure.Persistence;
using PDV.Infrastructure.Persistence.Interceptors;
using Xunit;

namespace PDV.Tests.Delivery;

public class OrderFulfillmentRoleFilteringTests
{
    private readonly DbContextOptions<AppDbContext> _dbContextOptions;

    public OrderFulfillmentRoleFilteringTests()
    {
        _dbContextOptions = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: $"PDV_FulfillmentFilter_Test_{Guid.NewGuid()}")
            .ConfigureWarnings(x => x.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .AddInterceptors(new DomainEventsInterceptor())
            .Options;
    }

    private async Task<(AppDbContext context, Guid branchId, string picker1Id, string picker2Id, string managerId)> SetupContextWithOrdersAsync()
    {
        var context = new AppDbContext(_dbContextOptions);

        var address = Address.Create("Av. Principal 123", "Centro", "CDMX", "06000", "México");
        var branch = new Branch("Sucursal Centro", "CEN01", address, "5551112233");
        context.Branches.Add(branch);

        var product = new Product("Producto Prueba", "PRD-01", 50m, SaleType.Piece, TaxRateType.ZeroRate, "General");
        context.Products.Add(product);
        await context.SaveChangesAsync(CancellationToken.None);

        string picker1Id = "picker-user-1";
        string picker2Id = "picker-user-2";
        string managerId = "manager-user-1";

        // 1. Pedido sin asignar (Pending, FilledById == null)
        var unassignedOrder = new Order(branch.Id, Guid.NewGuid(), PaymentMethodType.Cash, series: "PED", folio: 101, channel: OrderChannel.Telephone);
        unassignedOrder.AddItem(new OrderItem(product, 2m, 50m, 0m, isTaxExempt: true));

        // 2. Pedido asignado a Picker 1 (InFulfillment, FilledById == picker1Id)
        var picker1Order = new Order(branch.Id, Guid.NewGuid(), PaymentMethodType.Cash, series: "PED", folio: 102, channel: OrderChannel.Telephone);
        picker1Order.AddItem(new OrderItem(product, 1m, 50m, 0m, isTaxExempt: true));
        picker1Order.AssignPicker(picker1Id);

        // 3. Pedido asignado a Picker 2 (InFulfillment, FilledById == picker2Id)
        var picker2Order = new Order(branch.Id, Guid.NewGuid(), PaymentMethodType.Cash, series: "PED", folio: 103, channel: OrderChannel.Telephone);
        picker2Order.AddItem(new OrderItem(product, 3m, 50m, 0m, isTaxExempt: true));
        picker2Order.AssignPicker(picker2Id);

        // 4. Pedido asignado al Manager (InFulfillment, FilledById == managerId)
        var managerOrder = new Order(branch.Id, Guid.NewGuid(), PaymentMethodType.Cash, series: "PED", folio: 104, channel: OrderChannel.Telephone);
        managerOrder.AddItem(new OrderItem(product, 4m, 50m, 0m, isTaxExempt: true));
        managerOrder.AssignPicker(managerId);

        // 5. Pedido completado/surtido (Filled) - no debe aparecer en la cola de surtido de nadie
        var completedOrder = new Order(branch.Id, Guid.NewGuid(), PaymentMethodType.Cash, series: "PED", folio: 105, channel: OrderChannel.Telephone);
        completedOrder.AddItem(new OrderItem(product, 1m, 50m, 0m, isTaxExempt: true));
        completedOrder.AssignPicker(picker1Id);
        completedOrder.MarkAsFilled();

        context.Orders.AddRange(unassignedOrder, picker1Order, picker2Order, managerOrder, completedOrder);
        await context.SaveChangesAsync(CancellationToken.None);

        return (context, branch.Id, picker1Id, picker2Id, managerId);
    }

    [Fact]
    public async Task Handle_WhenStandardPickerUser_ReturnsOnlyOrdersAssignedToThatPicker()
    {
        // Arrange
        var (context, branchId, picker1Id, picker2Id, managerId) = await SetupContextWithOrdersAsync();
        var handler = new GetOrdersForFulfillmentQueryHandler(context);

        var query = new GetOrdersForFulfillmentQuery
        {
            BranchId = branchId,
            UserId = picker1Id,
            IsAdminOrManager = false
        };

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Single(result);
        Assert.Equal(102, result.First().Folio);
        Assert.Equal(picker1Id, result.First().FilledById);
    }

    [Fact]
    public async Task Handle_WhenAdminOrManagerUser_ReturnsUnassignedOrdersAndOwnAssignedOrders_ExcludingOtherPickers()
    {
        // Arrange
        var (context, branchId, picker1Id, picker2Id, managerId) = await SetupContextWithOrdersAsync();
        var handler = new GetOrdersForFulfillmentQueryHandler(context);

        var query = new GetOrdersForFulfillmentQuery
        {
            BranchId = branchId,
            UserId = managerId,
            IsAdminOrManager = true
        };

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        // Debe contener el pedido sin asignar (101) y el pedido tomado por el manager (104)
        Assert.Equal(2, result.Count);
        Assert.Contains(result, o => o.Folio == 101 && string.IsNullOrEmpty(o.FilledById));
        Assert.Contains(result, o => o.Folio == 104 && o.FilledById == managerId);

        // NO debe contener pedidos asignados a otros surtidores (102, 103) ni el completado (105)
        Assert.DoesNotContain(result, o => o.Folio == 102);
        Assert.DoesNotContain(result, o => o.Folio == 103);
        Assert.DoesNotContain(result, o => o.Folio == 105);
    }

    [Fact]
    public async Task Handle_WhenUserIdNotSpecified_ReturnsAllPendingAndInFulfillmentOrders()
    {
        // Arrange
        var (context, branchId, _, _, _) = await SetupContextWithOrdersAsync();
        var handler = new GetOrdersForFulfillmentQueryHandler(context);

        var query = new GetOrdersForFulfillmentQuery
        {
            BranchId = branchId
        };

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        // Debe devolver los 4 pedidos activos (101, 102, 103, 104), omitiendo el completado (105)
        Assert.Equal(4, result.Count);
        Assert.Contains(result, o => o.Folio == 101);
        Assert.Contains(result, o => o.Folio == 102);
        Assert.Contains(result, o => o.Folio == 103);
        Assert.Contains(result, o => o.Folio == 104);
        Assert.DoesNotContain(result, o => o.Folio == 105);
    }
}
