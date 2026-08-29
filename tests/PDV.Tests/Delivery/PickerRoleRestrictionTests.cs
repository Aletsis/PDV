using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using Moq;
using PDV.Application.Common.Interfaces;
using PDV.Application.Common.Services;
using PDV.Application.Features.Pickers.Commands.SetPickerStatus;
using PDV.Application.Features.Pickers.Commands.UpdatePickerCapacity;
using PDV.Application.Features.Pickers.Queries.GetMyPickerStatus;
using PDV.Application.Features.Pickers.Queries.GetPickersStatus;
using PDV.Domain.Entities;
using PDV.Domain.Enums;
using PDV.Domain.Exceptions;
using PDV.Domain.ValueObjects;
using PDV.Infrastructure.Persistence;
using PDV.Infrastructure.Persistence.Interceptors;
using Xunit;

namespace PDV.Tests.Delivery;

public class PickerRoleRestrictionTests
{
    private readonly DbContextOptions<AppDbContext> _dbContextOptions;

    public PickerRoleRestrictionTests()
    {
        _dbContextOptions = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: $"PDV_PickerRestriction_Test_{Guid.NewGuid()}")
            .ConfigureWarnings(x => x.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .AddInterceptors(new DomainEventsInterceptor())
            .Options;
    }

    private async Task<(AppDbContext context, Guid branchId, string pickerUserId, string almacenUserId)> SetupTestContextAsync()
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

        string pickerUserId = "usr-picker-1";
        string almacenUserId = "usr-almacen-1";

        return (context, branch.Id, pickerUserId, almacenUserId);
    }

    private Mock<IIdentityService> CreateMockIdentityService(Guid branchId, string pickerUserId, string almacenUserId)
    {
        var mock = new Mock<IIdentityService>();

        var users = new List<UserSyncDataDto>
        {
            new()
            {
                Id = pickerUserId,
                UserName = "surtidor.juan",
                FullName = "Juan Surtidor",
                IsActive = true,
                BranchId = branchId,
                Roles = new List<string> { "Picker" }
            },
            new()
            {
                Id = almacenUserId,
                UserName = "almacen.pedro",
                FullName = "Pedro Almacén",
                IsActive = true,
                BranchId = branchId,
                Roles = new List<string> { "Almacen" }
            }
        };

        mock.Setup(i => i.GetUsersAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(users);

        mock.Setup(i => i.GetUserByIdAsync(pickerUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(users.First(u => u.Id == pickerUserId));

        mock.Setup(i => i.GetUserByIdAsync(almacenUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(users.First(u => u.Id == almacenUserId));

        return mock;
    }

    [Fact]
    public async Task GetPickersStatusQuery_ShouldOnlyReturnUsersWithPickerRole_ExcludingAlmacen()
    {
        // Arrange
        var (context, branchId, pickerUserId, almacenUserId) = await SetupTestContextAsync();
        var identityMock = CreateMockIdentityService(branchId, pickerUserId, almacenUserId);

        var handler = new GetPickersStatusQueryHandler(context, identityMock.Object);

        // Act
        var result = await handler.Handle(new GetPickersStatusQuery(branchId), CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Single(result);
        Assert.Equal(pickerUserId, result.First().UserId);
        Assert.DoesNotContain(result, p => p.UserId == almacenUserId);
    }

    [Fact]
    public async Task GetMyPickerStatusQuery_ShouldReturnNull_ForUserWithAlmacenRole()
    {
        // Arrange
        var (context, branchId, pickerUserId, almacenUserId) = await SetupTestContextAsync();
        var identityMock = CreateMockIdentityService(branchId, pickerUserId, almacenUserId);

        var handler = new GetMyPickerStatusQueryHandler(context, identityMock.Object);

        // Act
        var almacenStatus = await handler.Handle(new GetMyPickerStatusQuery(almacenUserId, branchId), CancellationToken.None);
        var pickerStatus = await handler.Handle(new GetMyPickerStatusQuery(pickerUserId, branchId), CancellationToken.None);

        // Assert
        Assert.Null(almacenStatus);
        Assert.NotNull(pickerStatus);
        Assert.Equal(pickerUserId, pickerStatus!.UserId);
    }

    [Fact]
    public async Task TryAssignPendingOrderAsync_ShouldNeverAssignToAlmacenUser_EvenIfWorkStatusIsAvailable()
    {
        // Arrange
        var (context, branchId, pickerUserId, almacenUserId) = await SetupTestContextAsync();
        var identityMock = CreateMockIdentityService(branchId, pickerUserId, almacenUserId);
        var loggerMock = Mock.Of<ILogger<PickerDispatcherService>>();

        // Ambos usuarios tienen registro de UserWorkStatus en estado Available
        var pickerWorkStatus = new UserWorkStatus(pickerUserId, branchId);
        pickerWorkStatus.SetAvailable();

        var almacenWorkStatus = new UserWorkStatus(almacenUserId, branchId);
        almacenWorkStatus.SetAvailable();

        context.UserWorkStatuses.AddRange(pickerWorkStatus, almacenWorkStatus);

        // Crear pedido pendiente
        var order = new Order(branchId, Guid.NewGuid(), PaymentMethodType.Cash, series: "PED-001", folio: 1, channel: OrderChannel.Telephone);
        context.Orders.Add(order);
        await context.SaveChangesAsync(CancellationToken.None);

        var dispatcher = new PickerDispatcherService(context, loggerMock, identityMock.Object);

        // Act
        var assigned = await dispatcher.TryAssignPendingOrderAsync(order.Id, CancellationToken.None);

        // Assert
        Assert.True(assigned);
        var updatedOrder = await context.Orders.FindAsync(order.Id);
        Assert.NotNull(updatedOrder);
        Assert.Equal(OrderStatus.InFulfillment, updatedOrder!.Status);
        Assert.Equal(pickerUserId, updatedOrder.FilledById);
        Assert.NotEqual(almacenUserId, updatedOrder.FilledById);
    }

    [Fact]
    public async Task TryAssignPendingOrderAsync_ShouldNotAssign_WhenOnlyAlmacenUsersAreAvailable()
    {
        // Arrange
        var (context, branchId, pickerUserId, almacenUserId) = await SetupTestContextAsync();
        var identityMock = CreateMockIdentityService(branchId, pickerUserId, almacenUserId);
        var loggerMock = Mock.Of<ILogger<PickerDispatcherService>>();

        // Solo el usuario Almacén está "Available"
        var almacenWorkStatus = new UserWorkStatus(almacenUserId, branchId);
        almacenWorkStatus.SetAvailable();

        // El Picker está fuera de turno
        var pickerWorkStatus = new UserWorkStatus(pickerUserId, branchId);
        pickerWorkStatus.SetOffDuty();

        context.UserWorkStatuses.AddRange(pickerWorkStatus, almacenWorkStatus);

        var order = new Order(branchId, Guid.NewGuid(), PaymentMethodType.Cash, series: "PED-002", folio: 2, channel: OrderChannel.Telephone);
        context.Orders.Add(order);
        await context.SaveChangesAsync(CancellationToken.None);

        var dispatcher = new PickerDispatcherService(context, loggerMock, identityMock.Object);

        // Act
        var assigned = await dispatcher.TryAssignPendingOrderAsync(order.Id, CancellationToken.None);

        // Assert
        Assert.False(assigned);
        var updatedOrder = await context.Orders.FindAsync(order.Id);
        Assert.NotNull(updatedOrder);
        Assert.Equal(OrderStatus.Pending, updatedOrder!.Status);
        Assert.Null(updatedOrder.FilledById);
    }

    [Fact]
    public async Task TryAssignNextPendingOrdersToPickerAsync_ShouldReturnZero_ForAlmacenUser()
    {
        // Arrange
        var (context, branchId, pickerUserId, almacenUserId) = await SetupTestContextAsync();
        var identityMock = CreateMockIdentityService(branchId, pickerUserId, almacenUserId);
        var loggerMock = Mock.Of<ILogger<PickerDispatcherService>>();

        var almacenWorkStatus = new UserWorkStatus(almacenUserId, branchId);
        almacenWorkStatus.SetAvailable();
        context.UserWorkStatuses.Add(almacenWorkStatus);

        var order = new Order(branchId, Guid.NewGuid(), PaymentMethodType.Cash, series: "PED-003", folio: 3, channel: OrderChannel.Telephone);
        context.Orders.Add(order);
        await context.SaveChangesAsync(CancellationToken.None);

        var dispatcher = new PickerDispatcherService(context, loggerMock, identityMock.Object);

        // Act
        var count = await dispatcher.TryAssignNextPendingOrdersToPickerAsync(almacenUserId, branchId, CancellationToken.None);

        // Assert
        Assert.Equal(0, count);
        var updatedOrder = await context.Orders.FindAsync(order.Id);
        Assert.Equal(OrderStatus.Pending, updatedOrder!.Status);
        Assert.Null(updatedOrder.FilledById);
    }

    [Fact]
    public async Task SetPickerStatusCommand_ShouldThrowDomainException_ForAlmacenUser()
    {
        // Arrange
        var (context, branchId, pickerUserId, almacenUserId) = await SetupTestContextAsync();
        var identityMock = CreateMockIdentityService(branchId, pickerUserId, almacenUserId);
        var dispatcherMock = Mock.Of<IPickerDispatcherService>();

        var handler = new SetPickerStatusCommandHandler(context, dispatcherMock, identityMock.Object);

        // Act & Assert
        await Assert.ThrowsAsync<DomainException>(() =>
            handler.Handle(new SetPickerStatusCommand
            {
                UserId = almacenUserId,
                BranchId = branchId,
                Status = PickerAvailabilityStatus.Available
            }, CancellationToken.None));

        // Para Picker debe completarse exitosamente
        var success = await handler.Handle(new SetPickerStatusCommand
        {
            UserId = pickerUserId,
            BranchId = branchId,
            Status = PickerAvailabilityStatus.Available
        }, CancellationToken.None);

        Assert.True(success);
    }

    [Fact]
    public async Task UpdatePickerCapacityCommand_ShouldThrowDomainException_ForAlmacenUser()
    {
        // Arrange
        var (context, branchId, pickerUserId, almacenUserId) = await SetupTestContextAsync();
        var identityMock = CreateMockIdentityService(branchId, pickerUserId, almacenUserId);
        var dispatcherMock = Mock.Of<IPickerDispatcherService>();

        var handler = new UpdatePickerCapacityCommandHandler(context, dispatcherMock, identityMock.Object);

        // Act & Assert
        await Assert.ThrowsAsync<DomainException>(() =>
            handler.Handle(new UpdatePickerCapacityCommand
            {
                UserId = almacenUserId,
                BranchId = branchId,
                MaxConcurrentOrders = 3
            }, CancellationToken.None));

        var success = await handler.Handle(new UpdatePickerCapacityCommand
        {
            UserId = pickerUserId,
            BranchId = branchId,
            MaxConcurrentOrders = 3
        }, CancellationToken.None);

        Assert.True(success);
    }
}
