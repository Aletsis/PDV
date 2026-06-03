using MediatR;
using Microsoft.EntityFrameworkCore;
using PDV.Application.Common.Interfaces;
using PDV.Domain.Entities;
using PDV.Domain.ValueObjects;
using PDV.Domain.Enums;

namespace PDV.Application.Features.Sales.Commands.CashCut;

public record CashCutCommand(
    Guid CashRegisterId, 
    string UserId, 
    decimal InitialCash, 
    decimal SalesTotal, 
    decimal CashInDrawer
) : IRequest<Guid>;

public class CashCutCommandHandler : IRequestHandler<CashCutCommand, Guid>
{
    private readonly IApplicationDbContext _context;

    public CashCutCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Guid> Handle(CashCutCommand request, CancellationToken cancellationToken)
    {
        var activeShift = await _context.Shifts
            .FirstOrDefaultAsync(s => s.CashRegisterId == request.CashRegisterId && s.Status == ShiftStatus.Open, cancellationToken);

        if (activeShift == null)
        {
            throw new InvalidOperationException("No hay un turno activo para la caja registradora indicada.");
        }

        // Validar si existen notas abiertas o pendientes de cobro en este turno
        var hasUnpaidSales = await _context.Sales
            .AnyAsync(s => s.ShiftId == activeShift.Id && !s.IsPaid && !s.IsCancelled, cancellationToken);

        if (hasUnpaidSales)
        {
            throw new InvalidOperationException("No se puede realizar el corte de caja porque existen notas abiertas o pendientes de cobro en este turno.");
        }

        // Obtener transacciones del turno para realizar el cierre formal
        var sales = await _context.Sales
            .Include(s => s.Taxes)
            .Where(s => s.ShiftId == activeShift.Id && s.IsPaid)
            .ToListAsync(cancellationToken);

        var totalCashSales = sales.Where(s => s.PaymentMethod == PaymentMethodType.Cash).Sum(s => s.TotalAmount);

        var cashCollections = await _context.CashCollections
            .Where(c => c.ShiftId == activeShift.Id)
            .ToListAsync(cancellationToken);

        var totalInflows = cashCollections.Where(c => c.Reason.StartsWith("[INFLOW]")).Sum(c => c.Amount);
        var totalOutflows = cashCollections.Where(c => c.Reason.StartsWith("[OUTFLOW]")).Sum(c => c.Amount);

        var totalCashReturns = await _context.Returns
            .Where(r => r.ShiftId == activeShift.Id && r.IsCompleted)
            .SumAsync(r => r.TotalRefund, cancellationToken);

        // Desglose por método de pago para el cierre del turno
        var paymentMethodTotals = sales
            .GroupBy(s => s.PaymentMethod)
            .Select(g => new PaymentMethodBreakdown(g.Key, g.Sum(s => s.TotalAmount)))
            .ToList();

        // Desglose de impuestos por tasa (IVA) sumando el desglose de todas las ventas del turno
        var salesTaxTotals = sales
            .SelectMany(s => s.Taxes)
            .GroupBy(t => new { t.Rate, t.IsExempt })
            .Select(g => new TaxBreakdown(
                Rate: g.Key.Rate,
                BaseAmount: g.Sum(t => t.BaseAmount),
                TaxAmount: g.Sum(t => t.TaxAmount),
                IsExempt: g.Key.IsExempt
            ))
            .ToList();

        // Obtener devoluciones para desglosar sus impuestos
        var returns = await _context.Returns
            .Include(r => r.Items)
            .Where(r => r.ShiftId == activeShift.Id && r.IsCompleted)
            .ToListAsync(cancellationToken);

        var returnsTaxTotals = returns
            .SelectMany(r => r.Items)
            .GroupBy(ri => new { ri.TaxRate, ri.IsTaxExempt })
            .Select(g => new TaxBreakdown(
                Rate: g.Key.TaxRate,
                BaseAmount: g.Sum(ri => ri.Subtotal),
                TaxAmount: g.Key.IsTaxExempt ? 0 : g.Sum(ri => ri.TotalTax),
                IsExempt: g.Key.IsTaxExempt
            ))
            .ToList();

        // Cerrar formalmente el turno en el dominio
        activeShift.Close(
            endTime: DateTime.UtcNow,
            totalCashSales: totalCashSales,
            totalCashReturns: totalCashReturns,
            totalInflows: totalInflows,
            totalOutflows: totalOutflows,
            paymentMethodTotals: paymentMethodTotals,
            salesTaxTotals: salesTaxTotals,
            returnsTaxTotals: returnsTaxTotals
        );

        var expectedCash = request.InitialCash + sales.Sum(s => s.TotalAmount) + totalInflows - totalCashReturns - totalOutflows;
        var denominations = DecomposeAmount(request.CashInDrawer);

        // Usar constructor de dominio para el arqueo/corte
        var cut = new PDV.Domain.Entities.CashCut(
            shiftId: activeShift.Id,
            cashRegisterId: request.CashRegisterId,
            userId: !string.IsNullOrEmpty(request.UserId) ? request.UserId : activeShift.UserId,
            systemExpectedCash: expectedCash,
            cashDenominations: denominations,
            declaredVouchers: new List<PaymentMethodBreakdown>()
        );

        _context.CashCuts.Add(cut);
        await _context.SaveChangesAsync(cancellationToken);

        return cut.Id;
    }

    private List<CashDenomination> DecomposeAmount(decimal amount)
    {
        var result = new List<CashDenomination>();
        var remaining = amount;

        if (remaining >= 1000m)
        {
            int qty = (int)(remaining / 1000m);
            result.Add(new CashDenomination(DenominationType.Bill_1000, qty));
            remaining %= 1000m;
        }
        if (remaining >= 500m)
        {
            int qty = (int)(remaining / 500m);
            result.Add(new CashDenomination(DenominationType.Bill_500, qty));
            remaining %= 500m;
        }
        if (remaining >= 200m)
        {
            int qty = (int)(remaining / 200m);
            result.Add(new CashDenomination(DenominationType.Bill_200, qty));
            remaining %= 200m;
        }
        if (remaining >= 100m)
        {
            int qty = (int)(remaining / 100m);
            result.Add(new CashDenomination(DenominationType.Bill_100, qty));
            remaining %= 100m;
        }
        if (remaining >= 50m)
        {
            int qty = (int)(remaining / 50m);
            result.Add(new CashDenomination(DenominationType.Bill_50, qty));
            remaining %= 50m;
        }
        if (remaining >= 20m)
        {
            int qty = (int)(remaining / 20m);
            result.Add(new CashDenomination(DenominationType.Bill_20, qty));
            remaining %= 20m;
        }
        if (remaining >= 10m)
        {
            int qty = (int)(remaining / 10m);
            result.Add(new CashDenomination(DenominationType.Coin_10, qty));
            remaining %= 10m;
        }
        if (remaining >= 5m)
        {
            int qty = (int)(remaining / 5m);
            result.Add(new CashDenomination(DenominationType.Coin_5, qty));
            remaining %= 5m;
        }
        if (remaining >= 2m)
        {
            int qty = (int)(remaining / 2m);
            result.Add(new CashDenomination(DenominationType.Coin_2, qty));
            remaining %= 2m;
        }
        if (remaining >= 1m)
        {
            int qty = (int)(remaining / 1m);
            result.Add(new CashDenomination(DenominationType.Coin_1, qty));
            remaining %= 1m;
        }
        if (remaining > 0)
        {
            int qty = (int)Math.Round(remaining / 0.10m);
            if (qty > 0)
            {
                result.Add(new CashDenomination(DenominationType.Coin_020, qty));
            }
        }

        return result;
    }
}
