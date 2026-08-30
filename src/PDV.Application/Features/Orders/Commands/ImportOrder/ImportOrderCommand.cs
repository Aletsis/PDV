using MediatR;
using Microsoft.EntityFrameworkCore;
using PDV.Application.Common.Interfaces;
using PDV.Application.Features.Orders.Dtos;
using PDV.Domain.Entities;
using PDV.Domain.ValueObjects;
using PDV.Domain.Enums;

namespace PDV.Application.Features.Orders.Commands.ImportOrder;

public record ImportOrderCommand(OrderDetailDto OrderDetail) : IRequest<bool>;

public class ImportOrderCommandHandler : IRequestHandler<ImportOrderCommand, bool>
{
    private readonly IApplicationDbContext _context;

    public ImportOrderCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<bool> Handle(ImportOrderCommand request, CancellationToken cancellationToken)
    {
        var dto = request.OrderDetail;

        // 1. Check if the sale already exists locally
        var existingOrder = await _context.Orders.AnyAsync(o => o.Id == dto.Id, cancellationToken);
        if (existingOrder)
        {
            return true;
        }

        // 2. Resolve client (fallback to null if not found locally)
        Guid? clientId = dto.ClientId;
        if (clientId.HasValue)
        {
            var clientExists = await _context.Clients.AnyAsync(c => c.Id == clientId.Value, cancellationToken);
            if (!clientExists)
            {
                clientId = null;
            }
        }

        // 3. Resolve branch
        Guid branchId = Guid.Empty;
        var branches = await _context.Branches.ToListAsync(cancellationToken);
        if (branches.Any())
        {
            branchId = branches.First().Id;
        }
        else
        {
            throw new InvalidOperationException("No se encontraron sucursales en la base de datos local.");
        }

        // 4. Resolve cash register if provided
        Guid? resolvedRegisterId = dto.CashRegisterId;
        if (resolvedRegisterId.HasValue)
        {
            var regExists = await _context.CashRegisters.AnyAsync(r => r.Id == resolvedRegisterId.Value, cancellationToken);
            if (!regExists)
            {
                resolvedRegisterId = null;
            }
        }

        // 5. Ensure the ShiftId exists locally if provided
        if (dto.ShiftId.HasValue && resolvedRegisterId.HasValue)
        {
            var shiftExists = await _context.Shifts.AnyAsync(s => s.Id == dto.ShiftId.Value, cancellationToken);
            if (!shiftExists)
            {
                // Insert a closed stub shift record to satisfy FK
                var stubShift = new Shift(
                    cashRegisterId: resolvedRegisterId.Value,
                    userId: "Sync",
                    initialCash: 0m
                );
                stubShift.SetId(dto.ShiftId.Value);

                // Close the shift so it doesn't affect active drawer operations
                stubShift.Close(
                    endTime: DateTime.Now,
                    totalCashSales: 0m,
                    totalCashReturns: 0m,
                    totalInflows: 0m,
                    totalOutflows: 0m,
                    paymentMethodTotals: new List<PaymentMethodBreakdown>(),
                    salesTaxTotals: new List<TaxBreakdown>(),
                    returnsTaxTotals: new List<TaxBreakdown>()
                );

                _context.Shifts.Add(stubShift);
                await _context.SaveChangesAsync(cancellationToken);
            }
        }

        // 6. Create the Order
        var paymentMethod = Enum.TryParse<PaymentMethodType>(dto.PaymentMethod, true, out var pm) ? pm : PaymentMethodType.Cash;

        var order = new Order(
            branchId: branchId,
            clientId: clientId,
            paymentMethod: paymentMethod,
            cashRegisterId: resolvedRegisterId,
            shiftId: dto.ShiftId,
            deliveryZoneId: null,
            takenById: "Sync",
            capturedById: "Sync",
            series: dto.Series,
            folio: dto.Folio,
            channel: dto.Channel
        );
        order.SetId(dto.Id);


        order.SetBranch(branchId);

        // 7. Add Items
        foreach (var item in dto.Items)
        {
            var product = await _context.Products.FindAsync(new object[] { item.ProductId }, cancellationToken);
            if (product == null)
            {
                throw new InvalidOperationException($"El producto '{item.ProductName}' ({item.ProductId}) no se encuentra registrado en esta caja.");
            }

            // Calculate tax details based on product configuration
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

            // Ensure the exact price is preserved (override if different from standard price)
            decimal? priceOverride = item.PriceOverride;
            if (!priceOverride.HasValue && item.UnitPrice != product.Price)
            {
                priceOverride = item.UnitPrice;
            }

            var orderItem = new OrderItem(
                product: product,
                quantity: item.Quantity,
                unitPrice: priceOverride ?? item.UnitPrice,
                taxRate: taxRatePercent,
                isTaxExempt: isExempt
            );

            orderItem.SetId(item.Id);
            order.AddItem(orderItem);
        }

        // 8. Finalize states if already paid, cancelled, or returned
        if (dto.IsCancelled)
        {
            order.Cancel("Importada como cancelada");
        }

        _context.Orders.Add(order);
        await _context.SaveChangesAsync(cancellationToken);

        return true;
    }
}