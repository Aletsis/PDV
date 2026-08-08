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

namespace PDV.Application.Features.Orders.Commands.CreateOrder;

public record OrderCartItemDto
{
    public Guid ProductId { get; set; }
    public decimal Quantity { get; set; }
    public decimal? PriceOverride { get; set; }
}

public record CreateOrderCommand : IRequest<Guid>
{
    public List<OrderCartItemDto> Items { get; set; } = new();
    public string PaymentMethod { get; set; } = "Cash";
    public string UserId { get; set; } = string.Empty;
    public Guid? ClientId { get; set; }
    public Guid? CashRegisterId { get; set; }
}

public class CreateOrderCommandHandler : IRequestHandler<CreateOrderCommand, Guid>
{
    private readonly IOrderRepository _orderRepository;
    private readonly IProductRepository _productRepository;
    private readonly ITicketSequenceRepository _ticketSequenceRepository;
    private readonly IApplicationDbContext _context;

    public CreateOrderCommandHandler(
        IOrderRepository orderRepository,
        IProductRepository productRepository,
        ITicketSequenceRepository ticketSequenceRepository,
        IApplicationDbContext context)
    {
        _orderRepository = orderRepository;
        _productRepository = productRepository;
        _ticketSequenceRepository = ticketSequenceRepository;
        _context = context;
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
                if (!request.CashRegisterId.HasValue)
                {
                    throw new DomainException("La caja registradora es requerida para registrar un pedido.");
                }

                // Buscar turno activo
                var activeShift = await _context.Shifts
                    .Include(s => s.CashRegister)
                    .FirstOrDefaultAsync(s => s.CashRegisterId == request.CashRegisterId.Value && s.Status == ShiftStatus.Open, cancellationToken);
                
                if (activeShift == null)
                {
                    throw new DomainException("No se puede registrar un pedido si no hay un turno de caja activo.");
                }

                var client = request.ClientId.HasValue
                    ? await _context.Clients.FindAsync(new object[] { request.ClientId.Value }, cancellationToken)
                    : null;

                var paymentMethod = Enum.TryParse<PaymentMethodType>(request.PaymentMethod, true, out var pm) ? pm : PaymentMethodType.Cash;

                // Obtener secuencia de folios para pedido
                var sequence = await _ticketSequenceRepository.GetWithLockAsync(request.CashRegisterId.Value, TicketSequenceType.Order, cancellationToken);
                if (sequence == null)
                {
                    sequence = new TicketSequence(request.CashRegisterId.Value, TicketSequenceType.Order, "PED");
                    await _ticketSequenceRepository.AddAsync(sequence, cancellationToken);
                }

                int nextFolio = sequence.GetNextTicketNumber();
                string series = sequence.Series ?? "PED";

                var order = new Order(
                    cashRegisterId: request.CashRegisterId.Value,
                    branchId: activeShift.CashRegister!.BranchId,
                    clientId: request.ClientId,
                    paymentMethod: paymentMethod,
                    deliveryZoneId: client?.DeliveryZoneId,
                    takenById: request.UserId,
                    capturedById: request.UserId,
                    series: series,
                    folio: nextFolio
                );

                await _ticketSequenceRepository.UpdateAsync(sequence, cancellationToken);

                // Agregar artículos
                foreach (var item in request.Items)
                {
                    var product = await _productRepository.GetByIdAsync(item.ProductId, cancellationToken);
                    if (product == null)
                        throw new DomainException($"Producto con ID {item.ProductId} no encontrado.");

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

                        if (!branchStock.HasStock(item.Quantity))
                        {
                            throw new DomainException($"Stock insuficiente para el producto {product.Name}. Disponible: {branchStock.Stock}, Requerido: {item.Quantity}");
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
                        quantity: item.Quantity,
                        unitPrice: item.PriceOverride ?? product.Price,
                        taxRate: taxRatePercent,
                        isTaxExempt: isExempt
                    );

                    order.AddItem(orderItem);

                    // Descontar inventario preventivo
                    if (product.ControlExistencia != ControlExistencia.SinControl && branchStock != null)
                    {
                        branchStock.ApplyMovement(-item.Quantity, InventoryMovementType.Sale, order.Id, "Reserva de Pedido");
                    }
                }

                // Confirmar el pedido inmediatamente (puesto que se captura ya pesado)
                order.Confirm();

                await _orderRepository.AddAsync(order, cancellationToken);
                await _context.SaveChangesAsync(cancellationToken);
                await _context.CommitTransactionAsync(cancellationToken);

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
