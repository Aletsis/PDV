using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Moq;
using PDV.Application.Common.Interfaces;
using PDV.Application.Features.Orders.Dtos;
using PDV.Application.Features.Orders.Queries.GetOrderMonitoring;
using PDV.Domain.Entities;
using PDV.Domain.Enums;
using PDV.Domain.ValueObjects;
using PDV.Infrastructure.Persistence;
using PDV.Infrastructure.Persistence.Interceptors;
using Xunit;

namespace PDV.Tests.Orders;

public class OrderMonitoringQueryTests
{
    private readonly DbContextOptions<AppDbContext> _dbContextOptions;
    private readonly Mock<IIdentityService> _identityServiceMock;

    public OrderMonitoringQueryTests()
    {
        _dbContextOptions = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: $"PDV_Monitoring_Test_{Guid.NewGuid()}")
            .ConfigureWarnings(x => x.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .AddInterceptors(new DomainEventsInterceptor())
            .Options;

        _identityServiceMock = new Mock<IIdentityService>();
        _identityServiceMock.Setup(s => s.GetUsersAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<UserSyncDataDto>
            {
                new UserSyncDataDto { Id = "user-tel-1", UserName = "tel1", FullName = "María Telefonista", EmployeeNumber = "TEL-01", Roles = new List<string> { "Telephonist" } },
                new UserSyncDataDto { Id = "user-picker-1", UserName = "surtidor1", FullName = "Juan Surtidor", EmployeeNumber = "SURT-01", Roles = new List<string> { "Picker" } },
                new UserSyncDataDto { Id = "user-verifier-1", UserName = "verif1", FullName = "Carlos Verificador", EmployeeNumber = "VER-01", Roles = new List<string> { "Verifier" } },
                new UserSyncDataDto { Id = "user-driver-1", UserName = "chofer1", FullName = "Pedro Repartidor", EmployeeNumber = "REP-01", Roles = new List<string> { "DeliveryMan" } },
                new UserSyncDataDto { Id = "user-sup-1", UserName = "sup1", FullName = "Laura Supervisora", EmployeeNumber = "SUP-01", Roles = new List<string> { "Manager" } }
            });
    }

    private async Task<(AppDbContext context, Branch branch, Client client, DeliveryZone zone, Product product)> SetupTestDataAsync()
    {
        var context = new AppDbContext(_dbContextOptions);

        var address = Address.Create("Av. Central 100", "Centro", "CDMX", "01000", "México");
        var branch = new Branch("Sucursal Norte", "NOR01", address, "5559876543");
        context.Branches.Add(branch);

        var client = new Client("CLI01", "Abarrotes Don Pepe", "XAXX010101000", "5551112233", "pepe@test.com", ClientType.Retail);
        context.Clients.Add(client);

        var zone = new DeliveryZone("Zona 1 - Centro", branch.Id, "[]", 50m);
        context.DeliveryZones.Add(zone);

        var product = new Product("Arroz 1kg", "ARR-01", 35.00m, SaleType.Piece, TaxRateType.ZeroRate, "Abarrotes");
        context.Products.Add(product);

        await context.SaveChangesAsync();
        return (context, branch, client, zone, product);
    }

    [Fact]
    public async Task GetOrderMonitoring_ShouldReturnAllActiveOrdersWithResolvedAssignees()
    {
        // Arrange
        var (context, branch, client, zone, product) = await SetupTestDataAsync();

        // Crear pedido 1: Pendiente
        var order1 = new Order(branch.Id, client.Id, PaymentMethodType.Cash, deliveryZoneId: zone.Id, takenById: "user-tel-1", series: "PED", folio: 101);
        order1.AddItem(new OrderItem(product, 2, product.Price, 0, false));
        context.Orders.Add(order1);

        // Crear pedido 2: En Surtido
        var order2 = new Order(branch.Id, client.Id, PaymentMethodType.CreditCard, deliveryZoneId: zone.Id, takenById: "user-tel-1", series: "PED", folio: 102);
        order2.AddItem(new OrderItem(product, 3, product.Price, 0, false));
        order2.AssignPicker("user-picker-1");
        context.Orders.Add(order2);

        // Crear pedido 3: Confirmado
        var order3 = new Order(branch.Id, client.Id, PaymentMethodType.Cash, deliveryZoneId: zone.Id, takenById: "user-tel-1", series: "PED", folio: 103);
        order3.AddItem(new OrderItem(product, 1, product.Price, 0, false));
        order3.AssignPicker("user-picker-1");
        order3.MarkAsFilled();
        order3.VerifyOrder("user-verifier-1");
        context.Orders.Add(order3);

        await context.SaveChangesAsync();

        var handler = new GetOrderMonitoringQueryHandler(context, _identityServiceMock.Object);

        // Act
        var result = await handler.Handle(new GetOrderMonitoringQuery
        {
            BranchId = branch.Id,
            OnlyActive = true
        }, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(3, result.Summary.ActiveOrders);
        Assert.Equal(1, result.Summary.PendingOrders);
        Assert.Equal(1, result.Summary.InFulfillmentOrders);
        Assert.Equal(1, result.Summary.ConfirmedOrders);
        Assert.Equal(3, result.Orders.Count);

        var pOrder = result.Orders.FirstOrDefault(o => o.Folio == 101);
        Assert.NotNull(pOrder);
        Assert.Equal(OrderStatus.Pending, pOrder.Status);
        Assert.Equal("María Telefonista", pOrder.TakenByName);
        Assert.Equal("Sin Surtidor Asignado", pOrder.CurrentAssigneeName);
        Assert.Equal("Abarrotes Don Pepe", pOrder.ClientName);
        Assert.Equal("Zona 1 - Centro", pOrder.DeliveryZoneName);

        var fOrder = result.Orders.FirstOrDefault(o => o.Folio == 102);
        Assert.NotNull(fOrder);
        Assert.Equal(OrderStatus.InFulfillment, fOrder.Status);
        Assert.Equal("Juan Surtidor", fOrder.FilledByName);
        Assert.Equal("Juan Surtidor", fOrder.CurrentAssigneeName);
        Assert.Equal("Surtidor", fOrder.CurrentAssigneeRole);
        Assert.NotNull(fOrder.FulfillmentStartedAt);

        var cOrder = result.Orders.FirstOrDefault(o => o.Folio == 103);
        Assert.NotNull(cOrder);
        Assert.Equal(OrderStatus.Confirmed, cOrder.Status);
        Assert.Equal("Carlos Verificador", cOrder.VerifiedByName);
        Assert.NotNull(cOrder.VerifiedAt);
    }

    [Fact]
    public async Task GetOrderMonitoring_ShouldFilterByZoneAndSearchTerm()
    {
        // Arrange
        var (context, branch, client, zone, product) = await SetupTestDataAsync();

        var order1 = new Order(branch.Id, client.Id, PaymentMethodType.Cash, deliveryZoneId: zone.Id, series: "PED", folio: 201, generalNotes: "Entregar por portón trasero");
        order1.AddItem(new OrderItem(product, 1, product.Price, 0, false));
        context.Orders.Add(order1);

        var client2 = new Client("CLI02", "Farmacia La Esperanza", "XAXX010101001", "5558889900", "farmacia@test.com", ClientType.Retail);
        context.Clients.Add(client2);
        var order2 = new Order(branch.Id, client2.Id, PaymentMethodType.Cash, deliveryZoneId: null, series: "PED", folio: 202);
        order2.AddItem(new OrderItem(product, 1, product.Price, 0, false));
        context.Orders.Add(order2);

        await context.SaveChangesAsync();

        var handler = new GetOrderMonitoringQueryHandler(context, _identityServiceMock.Object);

        // Act 1: Filter by search term "portón"
        var searchResult = await handler.Handle(new GetOrderMonitoringQuery
        {
            BranchId = branch.Id,
            SearchTerm = "portón"
        }, CancellationToken.None);

        // Assert 1
        Assert.Single(searchResult.Orders);
        Assert.Equal(201, searchResult.Orders[0].Folio);

        // Act 2: Filter by DeliveryZoneId
        var zoneResult = await handler.Handle(new GetOrderMonitoringQuery
        {
            BranchId = branch.Id,
            DeliveryZoneId = zone.Id
        }, CancellationToken.None);

        // Assert 2
        Assert.Single(zoneResult.Orders);
        Assert.Equal(201, zoneResult.Orders[0].Folio);
    }

    [Fact]
    public async Task GetOrderMonitoring_ShouldAccuratelyTrackCompleteLifecycleAndAverageTimes()
    {
        // Arrange
        var (context, branch, client, zone, product) = await SetupTestDataAsync();

        var order = new Order(branch.Id, client.Id, PaymentMethodType.Cash, deliveryZoneId: zone.Id, series: "PED", folio: 301);
        order.AddItem(new OrderItem(product, 2, product.Price, 0, false));
        
        // Simular ciclo completo
        order.AssignPicker("user-picker-1");
        order.MarkAsFilled();
        order.VerifyOrder("user-verifier-1");

        var route = new DeliveryRoute(branch.Id, zone.Id, "user-driver-1", 1);
        context.DeliveryRoutes.Add(route);
        route.AddOrder(order);
        route.Dispatch("user-driver-1");

        order.MarkAsDelivered();
        context.Orders.Add(order);
        await context.SaveChangesAsync();

        var handler = new GetOrderMonitoringQueryHandler(context, _identityServiceMock.Object);

        // Act
        var result = await handler.Handle(new GetOrderMonitoringQuery
        {
            BranchId = branch.Id,
            OnlyActive = false
        }, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        var deliveredOrder = result.Orders.FirstOrDefault(o => o.Folio == 301);
        Assert.NotNull(deliveredOrder);
        Assert.Equal(OrderStatus.Delivered, deliveredOrder.Status);
        Assert.Equal("Pedro Repartidor", deliveredOrder.DeliveryManName);
        Assert.Equal("REP-01", deliveredOrder.DeliveryManEmployeeNumber);
        Assert.NotNull(deliveredOrder.DispatchedAt);
        Assert.NotNull(deliveredOrder.DeliveredAt);
        Assert.Equal(1, deliveredOrder.DeliveryRouteFolio);
    }
}
