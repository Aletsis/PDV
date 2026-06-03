using MediatR;
using Microsoft.EntityFrameworkCore;
using PDV.Application.Common.Interfaces;
using PDV.Application.Features.Sales.Dtos;
using PDV.Domain.Entities;
using PDV.Domain.ValueObjects;
using PDV.Domain.Enums;

namespace PDV.Application.Features.Sales.Commands.ImportSale;

public record ImportSaleCommand(SaleDetailDto SaleDetail) : IRequest<bool>;

public class ImportSaleCommandHandler : IRequestHandler<ImportSaleCommand, bool>
{
    private readonly IApplicationDbContext _context;

    public ImportSaleCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<bool> Handle(ImportSaleCommand request, CancellationToken cancellationToken)
    {
        var dto = request.SaleDetail;

        // 1. Check if the sale already exists locally
        var existingSale = await _context.Sales.AnyAsync(s => s.Id == dto.Id, cancellationToken);
        if (existingSale)
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

        // 3. Resolve cash register
        Guid? cashRegisterId = dto.ClientId; // Let's see if the DTO has CashRegisterId, wait, SaleDetailDto doesn't have CashRegisterId.
        // Wait! Let's check: does SaleDetailDto have CashRegisterId? No.
        // But we can check if there are any registers in the local DB.
        var registers = await _context.CashRegisters.ToListAsync(cancellationToken);
        Guid resolvedRegisterId;
        if (registers.Any())
        {
            resolvedRegisterId = registers.FirstOrDefault(r => r.IsActive)?.Id ?? registers.First().Id;
        }
        else
        {
            throw new InvalidOperationException("No se encontraron cajas registradoras en la base de datos local.");
        }

        // 4. Resolve branch
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

        // 5. Ensure the ShiftId exists locally to satisfy foreign key constraints
        var shiftExists = await _context.Shifts.AnyAsync(s => s.Id == dto.ShiftId, cancellationToken);
        if (!shiftExists)
        {
            // Insert a closed stub shift record to satisfy FK
            var stubShift = new Shift(
                cashRegisterId: resolvedRegisterId,
                userId: "Sync",
                initialCash: 0m
            );
            stubShift.SetId(dto.ShiftId);

            // Close the shift so it doesn't affect active drawer operations
            stubShift.Close(
                endTime: DateTime.UtcNow,
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

        // 6. Create the Sale
        var saleNumber = (SaleNumber)dto.SaleNumber;
        var paymentMethod = Enum.TryParse<PaymentMethodType>(dto.PaymentMethod, true, out var pm) ? pm : PaymentMethodType.Cash;

        var sale = new Sale(
            saleNumber: saleNumber,
            paymentMethod: paymentMethod,
            userId: "Sync",
            shiftId: dto.ShiftId,
            series: dto.Series,
            folio: dto.Folio,
            clientId: clientId,
            cashRegisterId: resolvedRegisterId
        );

        sale.SetBranch(branchId);

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

            var saleItem = new SaleItem(
                product: product,
                quantity: item.Quantity,
                taxRate: taxRatePercent,
                isTaxExempt: isExempt,
                priceOverride: priceOverride
            );

            saleItem.SetId(item.Id);
            sale.AddItem(saleItem);
        }

        // 8. Finalize states if already paid, cancelled, or returned
        if (dto.IsPaid)
        {
            sale.MarkAsPaid();
        }

        if (dto.IsCancelled)
        {
            sale.Cancel("Importada como cancelada");
        }

        if (dto.IsReturned)
        {
            sale.MarkAsReturned();
        }

        _context.Sales.Add(sale);
        await _context.SaveChangesAsync(cancellationToken);

        return true;
    }
}
