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
using PDV.Application.Features.DeliveryRoutes.Commands.SettleDeliveryRoute;
using PDV.Application.Features.DeliveryRoutes.Queries.GetMyActiveRoute;
using PDV.Application.Features.Orders.Commands.CompleteOrderFulfillment;
using PDV.Application.Features.Orders.Commands.ReportOrderDelivery;
using PDV.Application.Features.Orders.Commands.StartOrderFulfillment;
using PDV.Application.Features.Orders.Commands.TakeTelephonistOrder;
using PDV.Application.Features.Orders.Commands.VerifyAndConfirmOrder;
using PDV.Application.Features.Orders.Queries.GetOrdersForFulfillment;
using PDV.Application.Features.Orders.Queries.GetOrdersForVerification;
using PDV.Domain.Entities;
using PDV.Domain.Enums;
using PDV.Domain.ValueObjects;
using PDV.Infrastructure.Persistence;
using PDV.Infrastructure.Persistence.Interceptors;
using PDV.Infrastructure.Repositories;
using Xunit;

namespace PDV.Tests.Delivery;

public class OrderLifecycleWorkflowTests
{
    private readonly DbContextOptions<AppDbContext> _dbContextOptions;

    public OrderLifecycleWorkflowTests()
    {
        _dbContextOptions = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: $"PDV_Lifecycle_Test_{Guid.NewGuid()}")
            .ConfigureWarnings(x => x.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .AddInterceptors(new DomainEventsInterceptor())
            .Options;
    }

    private async Task<(AppDbContext context, Guid branchId, Product pieceProduct, Product bulkProduct, Guid registerId, Guid shiftId, Guid clientId, Guid zoneId)> SetupEnvironmentAsync()
    {
        var context = new AppDbContext(_dbContextOptions);

        var address = Address.Create("Av. Reforma 123", "Juárez", "CDMX", "06600", "México");
        var branch = new Branch("Sucursal Matriz", "MAT01", address, "5551234567");
        context.Branches.Add(branch);

        var register = new CashRegister("Caja 1", "CAJA01", branch.Id, CashRegisterMode.Orders);
        context.CashRegisters.Add(register);

        var shift = new Shift(register.Id, "cajero1", 1000m);
        context.Shifts.Add(shift);

        var pieceProduct = new Product("Leche Entera 1L", "LEC-01", 28.50m, SaleType.Piece, TaxRateType.ZeroRate, "Lácteos");
        var bulkProduct = new Product("Plátano Tabasco", "PLA-01", 22.00m, SaleType.Bulk, TaxRateType.ZeroRate, "Frutas");
        context.Products.AddRange(pieceProduct, bulkProduct);

        var stockPiece = new ProductBranchStock(pieceProduct.Id, branch.Id, 100m, 10m);
        var stockBulk = new ProductBranchStock(bulkProduct.Id, branch.Id, 50m, 5m);
        context.ProductBranchStocks.AddRange(stockPiece, stockBulk);

        var zone = new DeliveryZone("Zona Centro", branch.Id, "[]", 30m);
        context.DeliveryZones.Add(zone);

        var client = new Client("CLI01", "María González", "GOMA850101", "5559876543", "maria@test.com", ClientType.Retail);
        client.AssignDeliveryZone(zone.Id);
        context.Clients.Add(client);

        await context.SaveChangesAsync(CancellationToken.None);

        return (context, branch.Id, pieceProduct, bulkProduct, register.Id, shift.Id, client.Id, zone.Id);
    }

    [Fact]
    public async Task CompleteOrderLifecycle_FromTakeToSettlement_Succeeds()
    {
        // 1. Setup
        var (context, branchId, pieceProduct, bulkProduct, registerId, shiftId, clientId, zoneId) = await SetupEnvironmentAsync();
        var productRepository = new ProductRepository(context);
        var ticketSeqRepo = new TicketSequenceRepository(context);

        // 2. Telefonista: Toma el pedido
        var takeHandler = new TakeTelephonistOrderCommandHandler(context, productRepository);
        var takeCommand = new TakeTelephonistOrderCommand
        {
            BranchId = branchId,
            ClientId = clientId,
            DeliveryZoneId = zoneId,
            PaymentMethod = "Cash",
            UserId = "telefonista1",
            GeneralNotes = "Tocar timbre blanco",
            DeliveryNotes = "Dejar en caseta",
            Items = new List<TakeTelephonistOrderItemDto>
            {
                new() { ProductId = pieceProduct.Id, Quantity = 2m, Notes = "Fría" },
                new() { ProductId = bulkProduct.Id, Quantity = 1.5m, Notes = "Bien maduro" }
            }
        };

        var orderId = await takeHandler.Handle(takeCommand, CancellationToken.None);
        Assert.NotEqual(Guid.Empty, orderId);

        var savedOrder = await context.Orders.Include(o => o.Items).FirstOrDefaultAsync(o => o.Id == orderId);
        Assert.NotNull(savedOrder);
        Assert.Equal(OrderStatus.Pending, savedOrder.Status);
        Assert.Equal("telefonista1", savedOrder.TakenById);
        Assert.Null(savedOrder.CashRegisterId);
        Assert.Null(savedOrder.ShiftId);
        Assert.Equal(2, savedOrder.Items.Count);

        // 3. Surtidor: Consulta pedidos por surtir y toma el pedido
        var getFulfillmentHandler = new GetOrdersForFulfillmentQueryHandler(context);
        var pendingOrders = await getFulfillmentHandler.Handle(new GetOrdersForFulfillmentQuery { BranchId = branchId }, CancellationToken.None);
        Assert.Contains(pendingOrders, o => o.Id == orderId);

        var startFulfillmentHandler = new StartOrderFulfillmentCommandHandler(context);
        await startFulfillmentHandler.Handle(new StartOrderFulfillmentCommand(orderId, "surtidor1"), CancellationToken.None);

        var inProgressOrder = await context.Orders.FindAsync(orderId);
        Assert.Equal(OrderStatus.InFulfillment, inProgressOrder!.Status);
        Assert.Equal("surtidor1", inProgressOrder.FilledById);

        // Surtidor marca como surtido
        var completeFulfillmentHandler = new CompleteOrderFulfillmentCommandHandler(context);
        await completeFulfillmentHandler.Handle(new CompleteOrderFulfillmentCommand(orderId, "surtidor1"), CancellationToken.None);

        var filledOrder = await context.Orders.Include(o => o.Items).FirstOrDefaultAsync(o => o.Id == orderId);
        Assert.Equal(OrderStatus.Filled, filledOrder!.Status);
        Assert.True(filledOrder.Items.All(i => i.IsFulfilled));

        // 4. Verificador: Consulta pedidos surtidos, ajusta peso real de plátano en báscula y confirma
        var getVerificationHandler = new GetOrdersForVerificationQueryHandler(context);
        var verificationOrders = await getVerificationHandler.Handle(new GetOrdersForVerificationQuery { BranchId = branchId }, CancellationToken.None);
        Assert.Contains(verificationOrders, o => o.Id == orderId);

        var bulkItem = filledOrder.Items.First(i => i.ProductId == bulkProduct.Id);
        var verifyHandler = new VerifyAndConfirmOrderCommandHandler(context, ticketSeqRepo);
        var verifyCommand = new VerifyAndConfirmOrderCommand
        {
            OrderId = orderId,
            UserId = "cajero_verificador",
            CashRegisterId = registerId,
            ShiftId = shiftId,
            DeliveryZoneId = zoneId,
            UpdatedItems = new List<VerifyOrderItemDto>
            {
                new() { ItemId = bulkItem.Id, RealQuantity = 1.620m } // Báscula pesó 1.620 kg
            }
        };

        var verifyResult = await verifyHandler.Handle(verifyCommand, CancellationToken.None);
        Assert.True(verifyResult);

        var verifiedOrder = await context.Orders.Include(o => o.Items).FirstOrDefaultAsync(o => o.Id == orderId);
        Assert.True(verifiedOrder!.Status == OrderStatus.Confirmed || verifiedOrder.Status == OrderStatus.Routed);
        Assert.Equal("cajero_verificador", verifiedOrder.VerifiedById);
        Assert.Equal(registerId, verifiedOrder.CashRegisterId);
        Assert.Equal(shiftId, verifiedOrder.ShiftId);
        Assert.Equal(1.620m, verifiedOrder.Items.First(i => i.ProductId == bulkProduct.Id).Quantity);

        // 5. Repartidor: Consulta su ruta activa y reporta entrega
        var deliveryManId = "repartidor1";
        // Asignar repartidor a la ruta creada
        var route = await context.DeliveryRoutes.Include(r => r.Orders).FirstOrDefaultAsync(r => r.BranchId == branchId);
        Assert.NotNull(route);
        route.Dispatch(deliveryManId);
        await context.SaveChangesAsync(CancellationToken.None);

        var getMyRouteHandler = new GetMyActiveRouteQueryHandler(context);
        var myRoute = await getMyRouteHandler.Handle(new GetMyActiveRouteQuery(deliveryManId), CancellationToken.None);
        Assert.NotNull(myRoute);
        Assert.Equal(DeliveryRouteStatus.EnRoute, myRoute!.Status);

        // Repartidor marca pedido como entregado
        var reportDeliveryHandler = new ReportOrderDeliveryCommandHandler(context);
        await reportDeliveryHandler.Handle(new ReportOrderDeliveryCommand
        {
            OrderId = orderId,
            IsDelivered = true,
            DeliveryManId = deliveryManId
        }, CancellationToken.None);

        var deliveredOrder = await context.Orders.FindAsync(orderId);
        Assert.Equal(OrderStatus.Delivered, deliveredOrder!.Status);

        // 6. Liquidación de la ruta en caja
        var routeRepo = new DeliveryRouteRepository(context);
        var orderRepo = new OrderRepository(context);
        var saleRepo = new SaleRepository(context);

        var settleHandler = new SettleDeliveryRouteCommandHandler(routeRepo, orderRepo, saleRepo, ticketSeqRepo, context);
        var settleResult = await settleHandler.Handle(new SettleDeliveryRouteCommand
        {
            RouteId = route.Id,
            CashRegisterId = registerId,
            UserId = "cajero_verificador",
            Settlements = new List<OrderSettlementResultDto>
            {
                new() { OrderId = orderId, Delivered = true }
            }
        }, CancellationToken.None);

        Assert.True(settleResult);

        var settledOrder = await context.Orders.FindAsync(orderId);
        Assert.Equal(OrderStatus.Settled, settledOrder!.Status);

        var generatedSale = await context.Sales.Include(s => s.Items).FirstOrDefaultAsync(s => s.ShiftId == shiftId);
        Assert.NotNull(generatedSale);
        Assert.Equal(settledOrder.TotalAmount, generatedSale!.TotalAmount);
    }
}
