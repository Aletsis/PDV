using MediatR;
using Microsoft.EntityFrameworkCore;
using PDV.Application.Common.Interfaces;
using PDV.Domain.Enums;
using PDV.Domain.ValueObjects;

namespace PDV.Application.Features.Shifts.Queries.GetShiftTotals;

public record ShiftTotalsDto(
    Guid ShiftId,
    decimal InitialCash,
    decimal SalesTotal,
    decimal CashSalesTotal,
    decimal InflowsTotal,
    decimal OutflowsTotal,
    decimal ReturnsTotal,
    decimal ExpectedCash,
    IReadOnlyCollection<PaymentMethodBreakdown> PaymentMethods,
    IReadOnlyCollection<TaxBreakdown> Taxes
);

public record GetShiftTotalsQuery(Guid ShiftId) : IRequest<ShiftTotalsDto>;

public class GetShiftTotalsQueryHandler : IRequestHandler<GetShiftTotalsQuery, ShiftTotalsDto>
{
    private readonly IApplicationDbContext _context;

    public GetShiftTotalsQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<ShiftTotalsDto> Handle(GetShiftTotalsQuery request, CancellationToken cancellationToken)
    {
        var shift = await _context.Shifts.FindAsync(new object[] { request.ShiftId }, cancellationToken);
        if (shift == null)
        {
            throw new InvalidOperationException("Turno no encontrado.");
        }

        // Sumar ventas e incluir desgloses de impuestos propios de cada venta
        var sales = await _context.Sales
            .Include(s => s.Taxes)
            .Where(s => s.ShiftId == request.ShiftId && s.IsPaid)
            .ToListAsync(cancellationToken);

        var salesTotal = sales.Sum(s => s.TotalAmount);
        var cashSalesTotal = sales.Where(s => s.PaymentMethod == PaymentMethodType.Cash).Sum(s => s.TotalAmount);

        // Sumar movimientos de caja
        var cashCollections = await _context.CashCollections
            .Where(c => c.ShiftId == request.ShiftId)
            .ToListAsync(cancellationToken);

        var inflowsTotal = cashCollections.Where(c => c.Reason.StartsWith("[INFLOW]")).Sum(c => c.Amount);
        var outflowsTotal = cashCollections.Where(c => c.Reason.StartsWith("[OUTFLOW]")).Sum(c => c.Amount);

        // Sumar devoluciones
        var returnsTotal = await _context.Returns
            .Where(r => r.ShiftId == request.ShiftId && r.IsCompleted)
            .SumAsync(r => r.TotalRefund, cancellationToken);

        // Cálculo esperado en efectivo en caja
        var expectedCash = shift.InitialCash + cashSalesTotal + inflowsTotal - returnsTotal - outflowsTotal;

        // Desglose por método de pago para el turno actual
        var paymentMethods = sales
            .GroupBy(s => s.PaymentMethod)
            .Select(g => new PaymentMethodBreakdown(g.Key, g.Sum(s => s.TotalAmount)))
            .ToList();

        // Desglose de impuestos por tasa (IVA) sumando el desglose de todas las ventas del turno
        var taxes = sales
            .SelectMany(s => s.Taxes)
            .GroupBy(t => new { t.Rate, t.IsExempt })
            .Select(g => new TaxBreakdown(
                Rate: g.Key.Rate,
                BaseAmount: g.Sum(t => t.BaseAmount),
                TaxAmount: g.Sum(t => t.TaxAmount),
                IsExempt: g.Key.IsExempt
            ))
            .ToList();

        return new ShiftTotalsDto(
            ShiftId: request.ShiftId,
            InitialCash: shift.InitialCash,
            SalesTotal: salesTotal,
            CashSalesTotal: cashSalesTotal,
            InflowsTotal: inflowsTotal,
            OutflowsTotal: outflowsTotal,
            ReturnsTotal: returnsTotal,
            ExpectedCash: expectedCash,
            PaymentMethods: paymentMethods,
            Taxes: taxes
        );
    }
}
