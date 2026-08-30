using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Moq;
using PDV.Application.Common.Interfaces;
using PDV.Application.Features.DeliveryRoutes.Commands.CreateDeliveryRoute;
using PDV.Application.Features.Drivers.Commands.SetDriverStatus;
using PDV.Application.Features.Drivers.Queries.GetDriversStatus;
using PDV.Application.Features.Drivers.Queries.GetMyDriverStatus;
using PDV.Domain.Entities;
using PDV.Domain.Enums;
using PDV.Domain.Exceptions;
using PDV.Domain.Repositories;
using PDV.Domain.ValueObjects;
using PDV.Infrastructure.Persistence;
using PDV.Infrastructure.Persistence.Interceptors;
using PDV.Infrastructure.Repositories;
using Xunit;

namespace PDV.Tests.Delivery;

public class DriverRoleRestrictionTests
{
    private readonly DbContextOptions<AppDbContext> _dbContextOptions;

    public DriverRoleRestrictionTests()
    {
        _dbContextOptions = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: $"PDV_DriverRestriction_Test_{Guid.NewGuid()}")
            .ConfigureWarnings(x => x.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .AddInterceptors(new DomainEventsInterceptor())
            .Options;
    }

    private async Task<(AppDbContext context, Guid branchId, string driverUserId, string almacenUserId)> SetupTestContextAsync()
    {
        var context = new AppDbContext(_dbContextOptions);

        var address = Address.Create("Av. Principal 123", "Centro", "CDMX", "06000", "México");
        var branch = new Branch("Sucursal Centro", "CEN01", address, "5551112233");
        context.Branches.Add(branch);

        var product = new Product("Producto Prueba", "PRD-01", 50m, SaleType.Piece, TaxRateType.ZeroRate, "General");
        context.Products.Add(product);

        var stock = new ProductBranchStock(product.Id, branch.Id, 100m, 10m);
        context.ProductBranchStocks.Add(stock);

        await context.SaveChangesAsync(CancellationToken.None);

        string driverUserId = "usr-driver-1";
        string almacenUserId = "usr-almacen-1";

        return (context, branch.Id, driverUserId, almacenUserId);
    }

    private Mock<IIdentityService> CreateMockIdentityService(Guid branchId, string driverUserId, string almacenUserId)
    {
        var mock = new Mock<IIdentityService>();

        var users = new List<UserSyncDataDto>
        {
            new()
            {
                Id = driverUserId,
                UserName = "repartidor.carlos",
                FullName = "Carlos Repartidor",
                EmployeeNumber = "EMP-007",
                IsActive = true,
                BranchId = branchId,
                Roles = new List<string> { "DeliveryMan" }
            },
            new()
            {
                Id = almacenUserId,
                UserName = "almacen.pedro",
                FullName = "Pedro Almacén",
                EmployeeNumber = "EMP-008",
                IsActive = true,
                BranchId = branchId,
                Roles = new List<string> { "Almacen" }
            }
        };

        mock.Setup(i => i.GetUsersAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(users);

        mock.Setup(i => i.GetUserByIdAsync(driverUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(users.First(u => u.Id == driverUserId));

        mock.Setup(i => i.GetUserByIdAsync(almacenUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(users.First(u => u.Id == almacenUserId));

        return mock;
    }

    [Fact]
    public async Task GetDriversStatusQuery_ShouldOnlyReturnUsersWithDeliveryManRole_ExcludingAlmacen()
    {
        // Arrange
        var (context, branchId, driverUserId, almacenUserId) = await SetupTestContextAsync();
        var identityMock = CreateMockIdentityService(branchId, driverUserId, almacenUserId);

        var handler = new GetDriversStatusQueryHandler(context, identityMock.Object);

        // Act
        var result = await handler.Handle(new GetDriversStatusQuery(branchId), CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Single(result);
        Assert.Equal(driverUserId, result.First().UserId);
        Assert.DoesNotContain(result, d => d.UserId == almacenUserId);
    }

    [Fact]
    public async Task GetMyDriverStatusQuery_ShouldReturnNull_ForUserWithAlmacenRole()
    {
        // Arrange
        var (context, branchId, driverUserId, almacenUserId) = await SetupTestContextAsync();
        var identityMock = CreateMockIdentityService(branchId, driverUserId, almacenUserId);

        var handler = new GetMyDriverStatusQueryHandler(context, identityMock.Object);

        // Act
        var almacenStatus = await handler.Handle(new GetMyDriverStatusQuery(almacenUserId, branchId), CancellationToken.None);
        var driverStatus = await handler.Handle(new GetMyDriverStatusQuery(driverUserId, branchId), CancellationToken.None);

        // Assert
        Assert.Null(almacenStatus);
        Assert.NotNull(driverStatus);
        Assert.Equal(driverUserId, driverStatus!.UserId);
        Assert.Equal("Carlos Repartidor", driverStatus.FullName);
    }

    [Fact]
    public async Task SetDriverStatusCommand_ShouldThrowDomainException_ForAlmacenUser()
    {
        // Arrange
        var (context, branchId, driverUserId, almacenUserId) = await SetupTestContextAsync();
        var identityMock = CreateMockIdentityService(branchId, driverUserId, almacenUserId);

        var handler = new SetDriverStatusCommandHandler(context, identityMock.Object);

        // Act & Assert
        await Assert.ThrowsAsync<DomainException>(() =>
            handler.Handle(new SetDriverStatusCommand
            {
                UserId = almacenUserId,
                BranchId = branchId,
                Status = PickerAvailabilityStatus.Available
            }, CancellationToken.None));

        // Para Repartidor debe completarse exitosamente
        var success = await handler.Handle(new SetDriverStatusCommand
            {
                UserId = driverUserId,
                BranchId = branchId,
                Status = PickerAvailabilityStatus.Available
            }, CancellationToken.None);

        Assert.True(success);

        var status = await context.UserWorkStatuses.FirstOrDefaultAsync(s => s.UserId == driverUserId);
        Assert.NotNull(status);
        Assert.Equal(PickerAvailabilityStatus.Available, status!.Status);
    }

    [Fact]
    public async Task SetDriverStatusCommand_ShouldUpdateToMealBreakAndOperationalBreak()
    {
        // Arrange
        var (context, branchId, driverUserId, almacenUserId) = await SetupTestContextAsync();
        var identityMock = CreateMockIdentityService(branchId, driverUserId, almacenUserId);

        var handler = new SetDriverStatusCommandHandler(context, identityMock.Object);

        // Act 1: Comida
        await handler.Handle(new SetDriverStatusCommand
        {
            UserId = driverUserId,
            BranchId = branchId,
            Status = PickerAvailabilityStatus.MealBreak,
            Notes = "Comida en restaurante cercano"
        }, CancellationToken.None);

        var status = await context.UserWorkStatuses.FirstOrDefaultAsync(s => s.UserId == driverUserId);
        Assert.NotNull(status);
        Assert.Equal(PickerAvailabilityStatus.MealBreak, status!.Status);
        Assert.Equal("Comida en restaurante cercano", status.StatusNotes);

        // Act 2: Pausa operativa (Gasolina)
        await handler.Handle(new SetDriverStatusCommand
        {
            UserId = driverUserId,
            BranchId = branchId,
            Status = PickerAvailabilityStatus.OperationalBreak,
            Notes = "Carga de combustible"
        }, CancellationToken.None);

        var updatedStatus = await context.UserWorkStatuses.FirstOrDefaultAsync(s => s.UserId == driverUserId);
        Assert.NotNull(updatedStatus);
        Assert.Equal(PickerAvailabilityStatus.OperationalBreak, updatedStatus!.Status);
        Assert.Equal("Carga de combustible", updatedStatus.StatusNotes);
    }

    [Fact]
    public async Task GetDriversStatusQuery_ShouldIncludeActiveRouteStats()
    {
        // Arrange
        var (context, branchId, driverUserId, almacenUserId) = await SetupTestContextAsync();
        var identityMock = CreateMockIdentityService(branchId, driverUserId, almacenUserId);
        var product = await context.Products.FirstAsync();

        // Crear una ruta despachada (EnRoute) para el repartidor con 2 pedidos
        var route = new DeliveryRoute(branchId, null, driverUserId, 1);
        
        var order1 = new Order(branchId, Guid.NewGuid(), PaymentMethodType.Cash, series: "P01", folio: 1);
        order1.AddItem(new OrderItem(product, 1m, 50m, 0m, isTaxExempt: false));
        order1.Confirm();
        var order2 = new Order(branchId, Guid.NewGuid(), PaymentMethodType.Cash, series: "P01", folio: 2);
        order2.AddItem(new OrderItem(product, 1m, 50m, 0m, isTaxExempt: false));
        order2.Confirm();

        context.Orders.AddRange(order1, order2);
        route.AddOrder(order1);
        route.AddOrder(order2);
        route.Dispatch();

        context.DeliveryRoutes.Add(route);
        await context.SaveChangesAsync(CancellationToken.None);

        var handler = new GetDriversStatusQueryHandler(context, identityMock.Object);

        // Act
        var result = await handler.Handle(new GetDriversStatusQuery(branchId), CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        var driver = result.First(d => d.UserId == driverUserId);
        Assert.Equal(1, driver.ActiveRoutesCount);
        Assert.Equal(2, driver.ActiveOrdersCount);
        Assert.False(driver.IsEligible); // No elegible por tener ruta activa
    }

    [Fact]
    public async Task CreateDeliveryRouteCommand_ShouldUpdateDriverAssignedTimestamp()
    {
        // Arrange
        var (context, branchId, driverUserId, almacenUserId) = await SetupTestContextAsync();
        var identityMock = CreateMockIdentityService(branchId, driverUserId, almacenUserId);
        var product = await context.Products.FirstAsync();

        var order = new Order(branchId, Guid.NewGuid(), PaymentMethodType.Cash, series: "P01", folio: 1);
        order.AddItem(new OrderItem(product, 1m, 50m, 0m, isTaxExempt: false));
        order.Confirm();
        context.Orders.Add(order);
        await context.SaveChangesAsync(CancellationToken.None);

        var routeRepo = new DeliveryRouteRepository(context);
        var orderRepo = new OrderRepository(context);

        var handler = new CreateDeliveryRouteCommandHandler(routeRepo, orderRepo, identityMock.Object, context);

        // Act
        var routeId = await handler.Handle(new CreateDeliveryRouteCommand
        {
            BranchId = branchId,
            DeliveryManId = driverUserId,
            OrderIds = new List<Guid> { order.Id },
            CreatedBy = "admin"
        }, CancellationToken.None);

        // Assert
        Assert.NotEqual(Guid.Empty, routeId);
        var driverWorkStatus = await context.UserWorkStatuses.FirstOrDefaultAsync(s => s.UserId == driverUserId);
        Assert.NotNull(driverWorkStatus);
        Assert.NotNull(driverWorkStatus!.LastAssignedOrderAt);
    }
}
