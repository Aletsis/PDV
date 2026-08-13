using MediatR;
using Microsoft.EntityFrameworkCore;
using PDV.Application.Common.Interfaces;
using PDV.Application.Features.Sales.Dtos;
using PDV.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace PDV.Application.Features.Sales.Queries.GetReconciliationsList;

public enum ReconciliationFilterStatus
{
    All = 0,
    PendingOnly = 1,
    ReconciledOnly = 2
}

public record GetReconciliationsListQuery(
    DateTime? StartDate = null,
    DateTime? EndDate = null,
    Guid? CashRegisterId = null,
    ReconciliationFilterStatus FilterStatus = ReconciliationFilterStatus.All,
    Guid? BranchId = null
) : IRequest<ReconciliationsSummaryDto>;

public class GetReconciliationsListQueryHandler : IRequestHandler<GetReconciliationsListQuery, ReconciliationsSummaryDto>
{
    private readonly IApplicationDbContext _context;
    private readonly IIdentityService _identityService;

    public GetReconciliationsListQueryHandler(IApplicationDbContext context, IIdentityService identityService)
    {
        _context = context;
        _identityService = identityService;
    }

    public async Task<ReconciliationsSummaryDto> Handle(GetReconciliationsListQuery request, CancellationToken cancellationToken)
    {
        var shiftsQuery = _context.Shifts
            .Include(s => s.CashRegister)
            .Where(s => s.Status == ShiftStatus.Closed);

        if (request.CashRegisterId.HasValue && request.CashRegisterId.Value != Guid.Empty)
        {
            shiftsQuery = shiftsQuery.Where(s => s.CashRegisterId == request.CashRegisterId.Value);
        }

        if (request.BranchId.HasValue && request.BranchId.Value != Guid.Empty)
        {
            shiftsQuery = shiftsQuery.Where(s => s.CashRegister!.BranchId == request.BranchId.Value);
        }

        if (request.StartDate.HasValue)
        {
            var start = DateTime.SpecifyKind(request.StartDate.Value.Date, DateTimeKind.Utc);
            shiftsQuery = shiftsQuery.Where(s => s.StartTime >= start);
        }

        if (request.EndDate.HasValue)
        {
            var end = DateTime.SpecifyKind(request.EndDate.Value.Date.AddDays(1), DateTimeKind.Utc);
            shiftsQuery = shiftsQuery.Where(s => s.EndTime < end);
        }

        if (request.FilterStatus == ReconciliationFilterStatus.PendingOnly)
        {
            shiftsQuery = shiftsQuery.Where(s => !s.IsReconciled);
        }
        else if (request.FilterStatus == ReconciliationFilterStatus.ReconciledOnly)
        {
            shiftsQuery = shiftsQuery.Where(s => s.IsReconciled);
        }

        var shifts = await shiftsQuery
            .OrderByDescending(s => s.EndTime ?? s.StartTime)
            .ToListAsync(cancellationToken);

        var shiftIds = shifts.Select(s => s.Id).ToList();

        // Obtener cortes asociados si existen
        var cashCuts = await _context.CashCuts
            .Where(c => shiftIds.Contains(c.ShiftId))
            .ToDictionaryAsync(c => c.ShiftId, cancellationToken);

        // Obtener reconciliaciones existentes
        var reconciliations = await _context.CashCutReconciliations
            .Where(r => shiftIds.Contains(r.ShiftId))
            .ToDictionaryAsync(r => r.ShiftId, cancellationToken);

        // Diccionario de usuarios
        var usersDict = new Dictionary<string, string>();
        try
        {
            var users = await _identityService.GetUsersAsync(cancellationToken);
            usersDict = users.ToDictionary(u => u.Id, u => u.FullName);
        }
        catch
        {
            // Fallback si offline o error de identity
        }

        var resultItems = new List<ShiftReconciliationItemDto>();

        foreach (var shift in shifts)
        {
            var cashierName = GetUserName(shift.UserId, usersDict);
            cashCuts.TryGetValue(shift.Id, out var cut);
            reconciliations.TryGetValue(shift.Id, out var rec);

            // Obtener ventas asociadas al turno
            var sales = await _context.Sales
                .Where(s => s.ShiftId == shift.Id && s.IsPaid && !s.IsCancelled)
                .ToListAsync(cancellationToken);

            var totalSales = sales.Sum(s => s.TotalAmount);
            var cashSalesTotal = sales.Where(s => s.PaymentMethod == PaymentMethodType.Cash).Sum(s => s.TotalAmount);
            var cardSalesTotal = sales.Where(s => s.PaymentMethod == PaymentMethodType.CreditCard || s.PaymentMethod == PaymentMethodType.DebitCard).Sum(s => s.TotalAmount);

            // Obtener movimientos
            var cashCollections = await _context.CashCollections
                .Where(c => c.ShiftId == shift.Id)
                .ToListAsync(cancellationToken);

            var inflowsTotal = cashCollections.Where(c => c.Reason.StartsWith("[INFLOW]")).Sum(c => c.Amount);
            var outflowsTotal = cashCollections.Where(c => c.Reason.StartsWith("[OUTFLOW]")).Sum(c => c.Amount);

            // Obtener devoluciones
            var returnsTotal = await _context.Returns
                .Where(r => r.ShiftId == shift.Id && r.IsCompleted)
                .SumAsync(r => r.TotalRefund, cancellationToken);

            var expectedCash = shift.InitialCash + cashSalesTotal + inflowsTotal - returnsTotal - outflowsTotal;
            var expectedCard = cardSalesTotal;

            var item = new ShiftReconciliationItemDto
            {
                ShiftId = shift.Id,
                CashCutId = cut?.Id,
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
                IsReconciled = shift.IsReconciled || rec != null,
                ReconciliationId = rec?.Id,
                ReconciliationDate = rec?.ReconciliationDate ?? shift.ReconciledAt,
                ReconciledByUserId = rec?.ReconciledByUserId,
                ReconciledByName = rec != null ? GetUserName(rec.ReconciledByUserId, usersDict) : null,
                DeliveredCash = rec?.DeliveredCash,
                DeliveredCardVouchers = rec?.DeliveredCardVouchers,
                CashDifference = rec?.CashDifference,
                CardVouchersDifference = rec?.CardVouchersDifference,
                TotalDifference = rec?.TotalDifference,
                Status = rec?.Status,
                Notes = rec?.Notes
            };

            resultItems.Add(item);
        }

        var totalPending = resultItems.Count(i => !i.IsReconciled);
        var totalReconciled = resultItems.Count(i => i.IsReconciled);
        var totalBalanced = resultItems.Count(i => i.IsReconciled && i.Status == ReconciliationStatus.Balanced);
        var totalWithDiff = resultItems.Count(i => i.IsReconciled && i.Status.HasValue && i.Status != ReconciliationStatus.Balanced);
        var totalNetDiff = resultItems.Where(i => i.IsReconciled && i.TotalDifference.HasValue).Sum(i => i.TotalDifference!.Value);

        return new ReconciliationsSummaryDto
        {
            TotalShifts = resultItems.Count,
            TotalPending = totalPending,
            TotalReconciled = totalReconciled,
            TotalBalanced = totalBalanced,
            TotalWithDifference = totalWithDiff,
            TotalNetDifference = totalNetDiff,
            Items = resultItems
        };
    }

    private static string GetUserName(string? userId, Dictionary<string, string> usersDict)
    {
        if (string.IsNullOrEmpty(userId)) return "Desconocido";
        return usersDict.TryGetValue(userId, out var name) ? name : userId;
    }
}
