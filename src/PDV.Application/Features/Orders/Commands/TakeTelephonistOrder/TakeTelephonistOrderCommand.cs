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

namespace PDV.Application.Features.Orders.Commands.TakeTelephonistOrder;

public record TakeTelephonistOrderItemDto
{
    public Guid ProductId { get; set; }
    public decimal Quantity { get; set; }
    public decimal? PriceOverride { get; set; }
    public string? Notes { get; set; }
}

public record TakeTelephonistOrderCommand : IRequest<Guid>
{
    public Guid BranchId { get; set; }
    public Guid? ClientId { get; set; }
    public string PaymentMethod { get; set; } = "Cash";
    public string UserId { get; set; } = string.Empty;
    public Guid? DeliveryZoneId { get; set; }
    public bool IsOutOfZone { get; set; }
    public string? GeneralNotes { get; set; }
    public string? DeliveryNotes { get; set; }
    public List<TakeTelephonistOrderItemDto> Items { get; set; } = new();
}

public class TakeTelephonistOrderCommandHandler : IRequestHandler<TakeTelephonistOrderCommand, Guid>
{
    private readonly IApplicationDbContext _context;
    private readonly IProductRepository _productRepository;

    public TakeTelephonistOrderCommandHandler(
        IApplicationDbContext context,
        IProductRepository productRepository)
    {
        _context = context;
        _productRepository = productRepository;
    }

    public async Task<Guid> Handle(TakeTelephonistOrderCommand request, CancellationToken cancellationToken)
    {
        if (request.BranchId == Guid.Empty)
            throw new DomainException("La sucursal es requerida para registrar el pedido.");

        if (request.Items == null || !request.Items.Any())
            throw new DomainException("El pedido debe contener al menos un producto.");

        await _context.BeginTransactionAsync(cancellationToken);

        try
        {
            var client = request.ClientId.HasValue
                ? await _context.Clients.FindAsync(new object[] { request.ClientId.Value }, cancellationToken)
                : null;

            var paymentMethod = Enum.TryParse<PaymentMethodType>(request.PaymentMethod, true, out var pm) 
                ? pm 
                : PaymentMethodType.Cash;

            var zoneId = request.DeliveryZoneId ?? client?.DeliveryZoneId;

            var order = new Order(
                branchId: request.BranchId,
                cashRegisterId: null,
                shiftId: null,
                clientId: request.ClientId,
                paymentMethod: paymentMethod,
                deliveryZoneId: zoneId,
                takenById: request.UserId,
                capturedById: null,
                series: "TEL",
                folio: 0,
                generalNotes: request.GeneralNotes,
                deliveryNotes: request.DeliveryNotes,
                isOutOfZone: request.IsOutOfZone
            );

            foreach (var item in request.Items)
            {
                var product = await _productRepository.GetByIdAsync(item.ProductId, cancellationToken);
                if (product == null)
                    throw new DomainException($"Producto con ID {item.ProductId} no encontrado.");

                if (!product.IsActive)
                    throw new DomainException($"El producto {product.Name} está inactivo.");

                var branchStock = await _context.ProductBranchStocks
                    .FirstOrDefaultAsync(s => s.ProductId == product.Id && s.BranchId == request.BranchId, cancellationToken);

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
                    isTaxExempt: isExempt,
                    notes: item.Notes,
                    requestedQuantity: item.Quantity
                );

                order.AddItem(orderItem);

                if (product.ControlExistencia != ControlExistencia.SinControl && branchStock != null)
                {
                    branchStock.ApplyMovement(-item.Quantity, InventoryMovementType.Sale, order.Id, "Reserva Pedido Telefónico");
                }
            }

            _context.Orders.Add(order);
            await _context.SaveChangesAsync(cancellationToken);
            await _context.CommitTransactionAsync(cancellationToken);

            return order.Id;
        }
        catch
        {
            await _context.RollbackTransactionAsync(cancellationToken);
            throw;
        }
    }
}
