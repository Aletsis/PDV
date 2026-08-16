using System;
using System.Linq;
using PDV.Domain.Entities;
using PDV.Domain.Enums;
using PDV.Domain.Events;
using PDV.Domain.Exceptions;
using Xunit;

namespace PDV.Tests.Domain.Orders;

public class OrderDomainTests
{
    private Product CreateProduct(string name, decimal price, SaleType saleType = SaleType.Piece)
    {
        return new Product(
            name: name,
            code: Guid.NewGuid().ToString().Substring(0, 8),
            price: price,
            saleType: saleType
        );
    }

    [Fact]
    public void Constructor_WithValidParameters_InitializesCorrectlyAndRaisesEvent()
    {
        // Arrange
        var cashRegisterId = Guid.NewGuid();
        var branchId = Guid.NewGuid();
        var clientId = Guid.NewGuid();

        // Act
        var order = new Order(
            cashRegisterId: cashRegisterId,
            branchId: branchId,
            shiftId: Guid.NewGuid(),
            clientId: clientId,
            paymentMethod: PaymentMethodType.CreditCard,
            takenById: "user-taken",
            capturedById: "user-captured",
            series: "P",
            folio: 201
        );

        // Assert
        Assert.Equal(cashRegisterId, order.CashRegisterId);
        Assert.Equal(branchId, order.BranchId);
        Assert.Equal(clientId, order.ClientId);
        Assert.Equal(PaymentMethodType.CreditCard, order.PaymentMethod);
        Assert.Equal("user-taken", order.TakenById);
        Assert.Equal("user-captured", order.CapturedById);
        Assert.Equal("P", order.Series);
        Assert.Equal(201, order.Folio);
        Assert.Equal(OrderStatus.Pending, order.Status);
        Assert.Equal(0m, order.Subtotal);

        var createdEvent = order.DomainEvents.OfType<OrderCreatedEvent>().FirstOrDefault();
        Assert.NotNull(createdEvent);
        Assert.Equal(order.Id, createdEvent!.OrderId);
        Assert.Equal(clientId, createdEvent.ClientId);
    }

    [Fact]
    public void AddItem_WhenPending_AddsItemAndRecalculatesTotals()
    {
        // Arrange
        var order = new Order(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), PaymentMethodType.Cash);

        var product = CreateProduct("Jabon", 15m);
        var item = new OrderItem(product, 2m, 15m, 16m);

        // Act
        order.AddItem(item);

        // Assert
        Assert.Single(order.Items);
        Assert.Equal(30m, order.Subtotal);
        Assert.Equal(4.80m, order.TotalTax);
        Assert.Equal(34.80m, order.TotalAmount);

        var addedEvent = order.DomainEvents.OfType<OrderItemAddedEvent>().FirstOrDefault();
        Assert.NotNull(addedEvent);
        Assert.Equal(order.Id, addedEvent!.OrderId);
        Assert.Equal(product.Id, addedEvent.ProductId);
    }

    [Fact]
    public void AddItem_WhenNotEditable_ThrowsDomainException()
    {
        // Arrange
        var order = new Order(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), PaymentMethodType.Cash);
        var product = CreateProduct("Jabon", 15m);
        order.AddItem(new OrderItem(product, 1m, 15m, 16m));
        order.Confirm(); // status -> Confirmed
        order.AssignRoute(Guid.NewGuid(), "supervisor-user");
        order.AssignDeliveryMan("delivery-1"); // status -> EnRoute

        // Act & Assert
        var exception = Assert.Throws<DomainException>(() => order.AddItem(new OrderItem(product, 1m, 15m, 16m)));
        Assert.Equal("No se pueden agregar artículos a un pedido que no está pendiente o capturado.", exception.Message);
    }


    [Fact]
    public void Confirm_UnderMinimumWithoutAuthorization_ThrowsDomainException()
    {
        // Arrange
        var order = new Order(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), PaymentMethodType.Cash);

        var product = CreateProduct("Jabon", 15m);
        order.AddItem(new OrderItem(product, 1m, 15m, 0m, isTaxExempt: true)); // TotalAmount = 15m

        // Act & Assert - Monto minimo 50, total es 15, no autorizado
        var exception = Assert.Throws<DomainException>(() => order.Confirm(minimumRequiredAmount: 50m));
        Assert.Contains("El pedido no alcanza el monto mínimo", exception.Message);
    }

    [Fact]
    public void Confirm_UnderMinimumWithAuthorization_ConfirmsSuccessfully()
    {
        // Arrange
        var order = new Order(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), PaymentMethodType.Cash);

        var product = CreateProduct("Jabon", 15m);
        order.AddItem(new OrderItem(product, 1m, 15m, 0m, isTaxExempt: true)); // TotalAmount = 15m

        // Act - Autorizar primero
        order.AuthorizeUnderMinimum("supervisor-77");
        order.Confirm(minimumRequiredAmount: 50m);

        // Assert
        Assert.Equal(OrderStatus.Confirmed, order.Status);
        Assert.Equal("supervisor-77", order.AuthorizedBySupervisorId);
    }

    [Fact]
    public void RouteAndDeliveryWorkflow_TransitionsStateCorrectly()
    {
        // Arrange
        var order = new Order(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), PaymentMethodType.Cash);

        var product = CreateProduct("Jabon", 15m);
        order.AddItem(new OrderItem(product, 2m, 15m, 0m, isTaxExempt: true));
        order.Confirm();

        // 1. Assign Route (Confirm -> Routed/Confirmed)
        var routeId = Guid.NewGuid();
        order.AssignRoute(routeId, "user-router");
        Assert.Equal(routeId, order.DeliveryRouteId);

        // 2. Assign Delivery Man (EnRoute)
        order.AssignDeliveryMan("delivery-1");
        Assert.Equal(OrderStatus.EnRoute, order.Status);
        Assert.Equal("delivery-1", order.DeliveryManId);

        // 3. Mark As Delivered (Delivered)
        order.MarkAsDelivered();
        Assert.Equal(OrderStatus.Delivered, order.Status);
    }

    [Fact]
    public void Cancel_DeliveredOrder_ThrowsDomainException()
    {
        // Arrange
        var order = new Order(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), PaymentMethodType.Cash);

        var product = CreateProduct("Jabon", 15m);
        order.AddItem(new OrderItem(product, 2m, 15m, 0m, isTaxExempt: true));
        order.Confirm();
        order.AssignRoute(Guid.NewGuid(), "user-router");
        order.AssignDeliveryMan("delivery-1");
        order.MarkAsDelivered();

        // Act & Assert
        var exception = Assert.Throws<DomainException>(() => order.Cancel("Cancelacion tardia"));
        Assert.Equal("Un pedido entregado no puede ser cancelado directamente.", exception.Message);
    }

    [Fact]
    public void RequestInvoice_WithoutClient_ThrowsDomainException()
    {
        // Arrange - Sin cliente
        var order = new Order(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), clientId: null, PaymentMethodType.Cash);


        // Act & Assert
        var exception = Assert.Throws<DomainException>(() => order.RequestInvoice());
        Assert.Equal("No se puede solicitar factura sin un cliente asociado.", exception.Message);
    }
}
