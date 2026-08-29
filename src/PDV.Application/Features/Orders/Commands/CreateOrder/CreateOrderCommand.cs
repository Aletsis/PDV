using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PDV.Application.Common.Interfaces;
using PDV.Domain.Entities;
using PDV.Domain.Enums;
using PDV.Domain.Exceptions;
using PDV.Domain.Repositories;
using PDV.Application.Features.Orders.Dtos;

namespace PDV.Application.Features.Orders.Commands.CreateOrder;

public record CreateOrderCommand : IRequest<Guid>
{
    public Guid? BranchId { get; set; }
    public OrderChannel Channel { get; set; } = OrderChannel.Store;
    public List<CartItemDto> Items { get; set; } = new();
    public string PaymentMethod { get; set; } = "Cash";
    public string UserId { get; set; } = string.Empty;
    public Guid? ClientId { get; set; }
    public Guid? CashRegisterId { get; set; }
    public bool IsOpen { get; set; }
    public bool RequiresInvoice { get; set; } = false;
}

public class CreateOrderCommandHandler : IRequestHandler<CreateOrderCommand, Guid>
{
    private readonly IOrderRepository _orderRepository;
    private readonly IProductRepository _productRepository;
    private readonly IApplicationDbContext _context;
    private readonly IPickerDispatcherService _pickerDispatcher;

    public CreateOrderCommandHandler(
        IOrderRepository orderRepository,
        IProductRepository productRepository,
        IApplicationDbContext context,
        IPickerDispatcherService pickerDispatcher)
    {
        _orderRepository = orderRepository;
        _productRepository = productRepository;
        _context = context;
        _pickerDispatcher = pickerDispatcher;
    }

    public async Task<Guid> Handle(CreateOrderCommand request, CancellationToken cancellationToken)
    {
        const int maxRetries = 3;
        int attempt = 0;

        while (true)
        {
            attempt++;

            if (_context is DbContext dbContext)
            {
                dbContext.ChangeTracker.Clear();
            }

            await _context.BeginTransactionAsync(cancellationToken);

            try
            {
                Guid branchId = request.BranchId ?? Guid.Empty;
                Guid? shiftId = null;

                if (branchId == Guid.Empty && request.CashRegisterId.HasValue)
                {
                    var cashReg = await _context.CashRegisters.FindAsync(new object[] { request.CashRegisterId.Value }, cancellationToken);
                    if (cashReg != null)
                    {
                        branchId = cashReg.BranchId;
                    }
                }

                if (branchId == Guid.Empty)
                {
                    var firstBranch = await _context.Branches.Select(b => b.Id).FirstOrDefaultAsync(cancellationToken);
                    if (firstBranch != Guid.Empty)
                    {
                        branchId = firstBranch;
                    }
                    else
                    {
                        throw new DomainException("La sucursal es requerida para registrar un pedido.");
                    }
                }

                if (request.CashRegisterId.HasValue)
                {
                    var activeShift = await _context.Shifts
                        .FirstOrDefaultAsync(s => s.CashRegisterId == request.CashRegisterId.Value && s.Status == ShiftStatus.Open, cancellationToken);
                    shiftId = activeShift?.Id;
                }

                var client = request.ClientId.HasValue
                    ? await _context.Clients.FindAsync(new object[] { request.ClientId.Value }, cancellationToken)
                    : null;

                var paymentMethod = Enum.TryParse<PaymentMethodType>(request.PaymentMethod, true, out var pm) ? pm : PaymentMethodType.Cash;

                // Obtener secuencia de folios de pedido por sucursal
                var branch = await _context.Branches.FindAsync(new object[] { branchId }, cancellationToken);
                string series = branch?.GetEffectiveOrderSeries() ?? "PED";
                int nextFolio = await _orderRepository.GetNextFolioAsync(branchId, cancellationToken);

                var order = new Order(
                    branchId: branchId,
                    cashRegisterId: request.CashRegisterId,
                    shiftId: shiftId,
                    clientId: request.ClientId,
                    paymentMethod: paymentMethod,
                    deliveryZoneId: client?.DeliveryZoneId,
                    takenById: request.UserId,
                    capturedById: request.UserId,
                    series: series,
                    folio: nextFolio,
                    channel: request.Channel
                );

                // Agregar artículos
                foreach (var item in request.Items)
                {
                    var product = await _productRepository.GetByIdAsync(item.Product.Id, cancellationToken);
                    if (product == null)
                        throw new DomainException($"Producto con ID {item.Product.Id} no encontrado.");

                    if (!product.IsActive)
                        throw new DomainException($"El producto {product.Name} no está activo.");

                    // Verificar stock
                    var branchStock = await _context.ProductBranchStocks
                        .FirstOrDefaultAsync(s => s.ProductId == product.Id && s.BranchId == order.BranchId, cancellationToken);
                    
                    if (product.ControlExistencia != ControlExistencia.SinControl)
                    {
                        if (branchStock == null)
                        {
                            throw new DomainException($"No se encontró inventario configurado para el producto {product.Name} en esta sucursal.");
                        }

                        if (!branchStock.HasStock(item.QuantityDisplay))
                        {
                            throw new DomainException($"Stock insuficiente para el producto {product.Name}. Disponible: {branchStock.Stock}, Requerido: {item.QuantityDisplay}");
                        }
                    }

                    decimal taxRatePercent = 0m;
                    bool isExempt = false;

                    switch (product.TaxRate)
                    {
                        case TaxRateType.Exempt:
                            isExempt = true;
                            break;
                        case TaxRateType.ZeroRate:
                            taxRatePercent = 0m;
                            break;
                        case TaxRateType.Rate8:
                            taxRatePercent = 8m;
                            break;
                        case TaxRateType.Rate16:
                            taxRatePercent = 16m;
                            break;
                    }

                    var orderItem = new OrderItem(
                        product: product,
                        quantity: item.QuantityDisplay,
                        unitPrice: item.PriceOverride ?? product.Price,
                        taxRate: taxRatePercent,
                        isTaxExempt: isExempt,
                        notes: item.Notes,
                        requestedQuantity: item.RequestedQuantity > 0 ? item.RequestedQuantity : item.QuantityDisplay
                    );

                    order.AddItem(orderItem);

                    // Descontar inventario preventivo
                    if (product.ControlExistencia != ControlExistencia.SinControl && branchStock != null)
                    {
                        branchStock.ApplyMovement(-item.QuantityDisplay, InventoryMovementType.Sale, order.Id, "Reserva de Pedido");
                    }
                }

                // Confirmar el pedido si no se especifica como abierto/borrador
                if (!request.IsOpen)
                {
                    order.Confirm();
                }

                await _orderRepository.AddAsync(order, cancellationToken);
                await _context.SaveChangesAsync(cancellationToken);
                await _context.CommitTransactionAsync(cancellationToken);

                if (order.Status == OrderStatus.Pending)
                {
                    await _pickerDispatcher.TryAssignPendingOrderAsync(order.Id, cancellationToken);
                }

                return order.Id;
            }
            catch (DbUpdateConcurrencyException) when (attempt < maxRetries)
            {
                await _context.RollbackTransactionAsync(cancellationToken);
                await Task.Delay(50 * attempt, cancellationToken);
                continue;
            }
            catch
            {
                await _context.RollbackTransactionAsync(cancellationToken);
                throw;
            }
        }
    }
}
