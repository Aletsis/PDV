using MediatR;
using Microsoft.EntityFrameworkCore;
using PDV.Application.Common.Interfaces;
using PDV.Application.Features.Sales.Dtos;
using PDV.Domain.Enums;
using PDV.Domain.ValueObjects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace PDV.Application.Features.Sales.Queries.GetShiftReconciliationDetail;

public record GetShiftReconciliationDetailQuery(Guid ShiftId) : IRequest<ShiftReconciliationDetailDto>;

public class GetShiftReconciliationDetailQueryHandler : IRequestHandler<GetShiftReconciliationDetailQuery, ShiftReconciliationDetailDto>
{
    private readonly IApplicationDbContext _context;
    private readonly IIdentityService _identityService;

    public GetShiftReconciliationDetailQueryHandler(IApplicationDbContext context, IIdentityService identityService)
    {
        _context = context;
        _identityService = identityService;
    }

    public async Task<ShiftReconciliationDetailDto> Handle(GetShiftReconciliationDetailQuery request, CancellationToken cancellationToken)
    {
        var shift = await _context.Shifts
            .Include(s => s.CashRegister)
            .FirstOrDefaultAsync(s => s.Id == request.ShiftId, cancellationToken);

        if (shift == null)
        {
            throw new KeyNotFoundException($"Turno con ID {request.ShiftId} no encontrado.");
        }

        var cashCut = await _context.CashCuts
            .FirstOrDefaultAsync(c => c.ShiftId == shift.Id, cancellationToken);

        var reconciliation = await _context.CashCutReconciliations
            .Include(r => r.CashDenominations)
            .FirstOrDefaultAsync(r => r.ShiftId == shift.Id, cancellationToken);

        // Ventas pagadas y no canceladas
        var sales = await _context.Sales
            .Where(s => s.ShiftId == shift.Id && s.IsPaid && !s.IsCancelled)
            .ToListAsync(cancellationToken);

        var totalSales = sales.Sum(s => s.TotalAmount);
        var cashSalesTotal = sales.Where(s => s.PaymentMethod == PaymentMethodType.Cash).Sum(s => s.TotalAmount);
        var cardSalesTotal = sales.Where(s => s.PaymentMethod == PaymentMethodType.CreditCard || s.PaymentMethod == PaymentMethodType.DebitCard).Sum(s => s.TotalAmount);

        // Desglose de formas de pago
        var paymentMethods = sales
            .GroupBy(s => s.PaymentMethod)
            .Select(g => new PaymentMethodBreakdown(g.Key, g.Sum(s => s.TotalAmount)))
            .ToList();

        // Movimientos de caja (inflows y outflows)
        var cashCollections = await _context.CashCollections
            .Where(c => c.ShiftId == shift.Id)
            .OrderBy(c => c.CollectionDate)
            .ToListAsync(cancellationToken);

        var inflowMovements = cashCollections
            .Where(c => c.Type == CashCollectionType.Morralla)
            .Select(c => new ShiftMovementDetailDto
            {
                Date = c.CollectionDate,
                Type = "Entrada de Morralla",
                Reason = c.Reason,
                Amount = c.Amount
            })
            .ToList();

        var outflowMovements = cashCollections
            .Where(c => c.Type == CashCollectionType.Recoleccion)
            .Select(c => new ShiftMovementDetailDto
            {
                Date = c.CollectionDate,
                Type = "Recolección / Retiro",
                Reason = c.Reason,
                Amount = c.Amount
            })
            .ToList();

        var inflowsTotal = inflowMovements.Sum(m => m.Amount);
        var outflowsTotal = outflowMovements.Sum(m => m.Amount);

        // Devoluciones
        var returnsTotal = await _context.Returns
            .Where(r => r.ShiftId == shift.Id && r.IsCompleted)
            .SumAsync(r => r.TotalRefund, cancellationToken);

        var expectedCash = shift.InitialCash + cashSalesTotal + inflowsTotal - returnsTotal - outflowsTotal;
        var expectedCard = cardSalesTotal;

        // Usuarios
        var usersDict = new Dictionary<string, string>();
        try
        {
            var users = await _identityService.GetUsersAsync(cancellationToken);
            usersDict = users.ToDictionary(u => u.Id, u => u.FullName);
        }
        catch { }

        var cashierName = usersDict.TryGetValue(shift.UserId, out var cn) ? cn : shift.UserId;
        string? reconciledByName = null;
        if (reconciliation != null && !string.IsNullOrEmpty(reconciliation.ReconciledByUserId))
        {
            reconciledByName = usersDict.TryGetValue(reconciliation.ReconciledByUserId, out var rbn) ? rbn : reconciliation.ReconciledByUserId;
        }

        var denominationsDto = new List<CashDenominationDto>();
        if (reconciliation != null && reconciliation.CashDenominations.Any())
        {
            denominationsDto = reconciliation.CashDenominations
                .Select(d => new CashDenominationDto
                {
                    Type = d.Type,
                    Quantity = d.Quantity,
                    UnitValue = d.Type.GetValue()
                })
                .ToList();
        }

        return new ShiftReconciliationDetailDto
        {
            ShiftId = shift.Id,
            CashCutId = cashCut?.Id,
            CashRegisterId = shift.CashRegisterId,
            CashRegisterName = shift.CashRegister?.Name ?? "Caja",
            CashierUserId = shift.UserId,
            CashierName = cashierName,
            StartTime = shift.StartTime,
            EndTime = shift.EndTime,
            InitialCash = shift.InitialCash,
            TotalSales = totalSales,
            CashSalesTotal = cashSalesTotal,
            CardSalesTotal = cardSalesTotal,
            InflowsTotal = inflowsTotal,
            OutflowsTotal = outflowsTotal,
            ReturnsTotal = returnsTotal,
            ExpectedCash = expectedCash,
            ExpectedCardVouchers = expectedCard,
            PaymentMethods = paymentMethods,
            InflowMovements = inflowMovements,
            OutflowMovements = outflowMovements,
            IsReconciled = shift.IsReconciled || reconciliation != null,
            ReconciliationId = reconciliation?.Id,
            ReconciliationDate = reconciliation?.ReconciliationDate ?? shift.ReconciledAt,
            ReconciledByUserId = reconciliation?.ReconciledByUserId,
            ReconciledByName = reconciledByName,
            DeliveredCash = reconciliation?.DeliveredCash,
            DeliveredCardVouchers = reconciliation?.DeliveredCardVouchers,
            CashDifference = reconciliation?.CashDifference,
            CardVouchersDifference = reconciliation?.CardVouchersDifference,
            TotalDifference = reconciliation?.TotalDifference,
            Status = reconciliation?.Status,
            Notes = reconciliation?.Notes,
            Denominations = denominationsDto
        };
    }
}
