using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Moq;
using PDV.Application.Common.Interfaces;
using PDV.Application.Features.DeliveryRoutes.Commands.CreateDeliveryRoute;
using PDV.Domain.Entities;
using PDV.Domain.Enums;
using PDV.Domain.Exceptions;
using PDV.Domain.ValueObjects;
using PDV.Infrastructure.Persistence;
using PDV.Infrastructure.Persistence.Interceptors;
using PDV.Infrastructure.Repositories;
using Xunit;

namespace PDV.Tests.Delivery;

public class CreateDeliveryRouteCommandHandlerTests
{
    private readonly DbContextOptions<AppDbContext> _dbContextOptions;

    public CreateDeliveryRouteCommandHandlerTests()
    {
        _dbContextOptions = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: $"PDV_Delivery_Test_{Guid.NewGuid()}")
            .ConfigureWarnings(x => x.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .AddInterceptors(new DomainEventsInterceptor())
            .Options;
    }

    private async Task<(AppDbContext context, Order order, Guid branchId)> SetupBaseDataAsync()
    {
        var context = new AppDbContext(_dbContextOptions);

        var address = Address.Create("Calle Principal 456", "Valle", "CDMX", "08100", "México");
        var branch = new Branch("Sucursal Norte", "SN001", address, "5559876543");
        context.Branches.Add(branch);

        // Crear un producto para el pedido
        var product = new Product("Refresco 2L", "REF-02", 25m, SaleType.Piece, TaxRateType.Rate16, "Bebidas");
        context.Products.Add(product);

        // Crear y confirmar el pedido
        var order = new Order(Guid.NewGuid(), branch.Id, Guid.NewGuid(), PaymentMethodType.Cash);
        order.AddItem(new OrderItem(product, 1m, 25m, 0m, isTaxExempt: false));
        order.Confirm();
        context.Orders.Add(order);

        await context.SaveChangesAsync(CancellationToken.None);

        return (context, order, branch.Id);
    }

    [Fact]
    public async Task Handle_WithValidDeliveryMan_CreatesRouteSuccessfully()
    {
        // Arrange
        var (context, order, branchId) = await SetupBaseDataAsync();
        var routeRepository = new DeliveryRouteRepository(context);
        var orderRepository = new OrderRepository(context);

        var deliveryManId = "delivery-man-123";
        var identityServiceMock = new Mock<IIdentityService>();
        identityServiceMock.Setup(x => x.GetUserByIdAsync(deliveryManId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserSyncDataDto
            {
                Id = deliveryManId,
                FullName = "Juan Pérez",
                BranchId = branchId,
                Roles = new List<string> { "DeliveryMan" }
            });

        var handler = new CreateDeliveryRouteCommandHandler(routeRepository, orderRepository, identityServiceMock.Object, context);

        var command = new CreateDeliveryRouteCommand
        {
            BranchId = branchId,
            DeliveryManId = deliveryManId,
            OrderIds = new List<Guid> { order.Id },
            CreatedBy = "test-user"
        };

        // Act
        var routeId = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.NotEqual(Guid.Empty, routeId);

        var route = await context.DeliveryRoutes.Include(r => r.Orders).FirstOrDefaultAsync(r => r.Id == routeId);
        Assert.NotNull(route);
        Assert.Equal(branchId, route!.BranchId);
        Assert.Equal(deliveryManId, route.DeliveryManId);
        var routeOrder = Assert.Single(route.Orders);
        Assert.Equal(order.Id, routeOrder.Id);
    }

    [Fact]
    public async Task Handle_WithInvalidRole_ThrowsDomainException()
    {
        // Arrange
        var (context, order, branchId) = await SetupBaseDataAsync();
        var routeRepository = new DeliveryRouteRepository(context);
        var orderRepository = new OrderRepository(context);

        var invalidManId = "cashier-user";
        var identityServiceMock = new Mock<IIdentityService>();
        identityServiceMock.Setup(x => x.GetUserByIdAsync(invalidManId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserSyncDataDto
            {
                Id = invalidManId,
                FullName = "Cajero Martínez",
                BranchId = branchId,
                Roles = new List<string> { "Cashier" } // Rol no es DeliveryMan/repartidor
            });

        var handler = new CreateDeliveryRouteCommandHandler(routeRepository, orderRepository, identityServiceMock.Object, context);

        var command = new CreateDeliveryRouteCommand
        {
            BranchId = branchId,
            DeliveryManId = invalidManId,
            OrderIds = new List<Guid> { order.Id },
            CreatedBy = "test-user"
        };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<DomainException>(() => handler.Handle(command, CancellationToken.None));
        Assert.Equal("El usuario seleccionado no tiene el rol de repartidor.", exception.Message);
    }

    [Fact]
    public async Task Handle_WithDifferentBranch_ThrowsDomainException()
    {
        // Arrange
        var (context, order, branchId) = await SetupBaseDataAsync();
        var routeRepository = new DeliveryRouteRepository(context);
        var orderRepository = new OrderRepository(context);

        var differentBranchId = Guid.NewGuid();
        var deliveryManId = "delivery-man-other";
        var identityServiceMock = new Mock<IIdentityService>();
        identityServiceMock.Setup(x => x.GetUserByIdAsync(deliveryManId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserSyncDataDto
            {
                Id = deliveryManId,
                FullName = "Pedro Gómez",
                BranchId = differentBranchId, // Sucursal diferente
                Roles = new List<string> { "repartidor" } // Rol válido en español
            });

        var handler = new CreateDeliveryRouteCommandHandler(routeRepository, orderRepository, identityServiceMock.Object, context);

        var command = new CreateDeliveryRouteCommand
        {
            BranchId = branchId, // Sucursal de la ruta
            DeliveryManId = deliveryManId,
            OrderIds = new List<Guid> { order.Id },
            CreatedBy = "test-user"
        };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<DomainException>(() => handler.Handle(command, CancellationToken.None));
        Assert.Equal("El repartidor debe pertenecer a la misma sucursal de la ruta.", exception.Message);
    }

    [Fact]
    public async Task Handle_WithNullBranch_ThrowsDomainException()
    {
        // Arrange
        var (context, order, branchId) = await SetupBaseDataAsync();
        var routeRepository = new DeliveryRouteRepository(context);
        var orderRepository = new OrderRepository(context);

        var deliveryManId = "delivery-man-null-branch";
        var identityServiceMock = new Mock<IIdentityService>();
        identityServiceMock.Setup(x => x.GetUserByIdAsync(deliveryManId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserSyncDataDto
            {
                Id = deliveryManId,
                FullName = "Pedro Gómez",
                BranchId = null, // Sin sucursal asignada
                Roles = new List<string> { "DeliveryMan" }
            });

        var handler = new CreateDeliveryRouteCommandHandler(routeRepository, orderRepository, identityServiceMock.Object, context);

        var command = new CreateDeliveryRouteCommand
        {
            BranchId = branchId,
            DeliveryManId = deliveryManId,
            OrderIds = new List<Guid> { order.Id },
            CreatedBy = "test-user"
        };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<DomainException>(() => handler.Handle(command, CancellationToken.None));
        Assert.Equal("El repartidor debe pertenecer a la misma sucursal de la ruta.", exception.Message);
    }
}
