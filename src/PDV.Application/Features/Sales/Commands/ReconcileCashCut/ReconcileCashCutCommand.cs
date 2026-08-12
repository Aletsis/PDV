using MediatR;
using Microsoft.EntityFrameworkCore;
using PDV.Application.Common.Interfaces;
using PDV.Application.Common.Security;
using PDV.Application.Features.Sales.Dtos;
using PDV.Domain.Entities;
using PDV.Domain.Enums;
using PDV.Domain.Exceptions;
using PDV.Domain.ValueObjects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace PDV.Application.Features.Sales.Commands.ReconcileCashCut;

[AuthorizeCommand("sales.reconcile_cash_cut")]
public record ReconcileCashCutCommand(
    Guid ShiftId,
    decimal DeliveredCash,
    decimal DeliveredCardVouchers,
    string? Notes = null,
    List<CashDenominationDto>? Denominations = null,
    string? SupervisorUserId = null
) : IRequest<ReconciliationResultDto>, ISupervisorAuthorizedTarget
{
    public string? AuthorizedByUserId { get; set; }
}

public class ReconcileCashCutCommandHandler : IRequestHandler<ReconcileCashCutCommand, ReconciliationResultDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public ReconcileCashCutCommandHandler(IApplicationDbContext context, ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<ReconciliationResultDto> Handle(ReconcileCashCutCommand request, CancellationToken cancellationToken)
    {
        var shift = await _context.Shifts
            .FirstOrDefaultAsync(s => s.Id == request.ShiftId, cancellationToken);

        if (shift == null)
        {
            throw new KeyNotFoundException($"Turno con ID {request.ShiftId} no encontrado.");
        }

        if (shift.Status != ShiftStatus.Closed)
        {
            throw new InvalidOperationException("El turno debe estar cerrado antes de poder realizar la conciliación.");
        }

        if (shift.IsReconciled)
        {
            throw new InvalidOperationException("El turno ya ha sido conciliado previamente.");
        }

        // Buscar corte si existe
        var cashCut = await _context.CashCuts
            .FirstOrDefaultAsync(c => c.ShiftId == shift.Id, cancellationToken);

        // Calcular valores esperados de sistema
        var sales = await _context.Sales
            .Where(s => s.ShiftId == shift.Id && s.IsPaid && !s.IsCancelled)
            .ToListAsync(cancellationToken);

        var totalSales = sales.Sum(s => s.TotalAmount);
        var cashSalesTotal = sales.Where(s => s.PaymentMethod == PaymentMethodType.Cash).Sum(s => s.TotalAmount);
        var cardSalesTotal = sales.Where(s => s.PaymentMethod == PaymentMethodType.CreditCard || s.PaymentMethod == PaymentMethodType.DebitCard).Sum(s => s.TotalAmount);

        var cashCollections = await _context.CashCollections
            .Where(c => c.ShiftId == shift.Id)
            .ToListAsync(cancellationToken);

        var inflowsTotal = cashCollections.Where(c => c.Reason.StartsWith("[INFLOW]")).Sum(c => c.Amount);
        var outflowsTotal = cashCollections.Where(c => c.Reason.StartsWith("[OUTFLOW]")).Sum(c => c.Amount);

        var returnsTotal = await _context.Returns
            .Where(r => r.ShiftId == shift.Id && r.IsCompleted)
            .SumAsync(r => r.TotalRefund, cancellationToken);

        var expectedCash = shift.InitialCash + cashSalesTotal + inflowsTotal - returnsTotal - outflowsTotal;
        var expectedCardVouchers = cardSalesTotal;

        var supervisorId = request.AuthorizedByUserId 
            ?? (!string.IsNullOrWhiteSpace(request.SupervisorUserId) ? request.SupervisorUserId : _currentUserService.UserId) 
            ?? "Encargada";

        List<CashDenomination>? denominations = null;
        if (request.Denominations != null && request.Denominations.Any())
        {
            denominations = request.Denominations
                .Where(d => d.Quantity > 0)
                .Select(d => new CashDenomination(d.Type, d.Quantity))
                .ToList();
        }

        var reconciliation = new CashCutReconciliation(
            shiftId: shift.Id,
            cashCutId: cashCut?.Id,
            cashRegisterId: shift.CashRegisterId,
            cashierUserId: shift.UserId,
            reconciledByUserId: supervisorId,
            initialCash: shift.InitialCash,
            cashSalesTotal: cashSalesTotal,
            cardSalesTotal: cardSalesTotal,
            inflowsTotal: inflowsTotal,
            outflowsTotal: outflowsTotal,
            returnsTotal: returnsTotal,
            expectedCash: expectedCash,
            expectedCardVouchers: expectedCardVouchers,
            deliveredCash: request.DeliveredCash,
            deliveredCardVouchers: request.DeliveredCardVouchers,
            notes: request.Notes,
            denominations: denominations
        );

        shift.MarkAsReconciled(reconciliation.ReconciliationDate);
        if (cashCut != null)
        {
            cashCut.MarkAsReconciled(reconciliation.ReconciliationDate);
        }

        _context.CashCutReconciliations.Add(reconciliation);
        await _context.SaveChangesAsync(cancellationToken);

        string message = BuildResultMessage(reconciliation);

        return new ReconciliationResultDto
        {
            ReconciliationId = reconciliation.Id,
            ShiftId = shift.Id,
            Status = reconciliation.Status,
            ExpectedCash = reconciliation.ExpectedCash,
            DeliveredCash = reconciliation.DeliveredCash,
            CashDifference = reconciliation.CashDifference,
            ExpectedCardVouchers = reconciliation.ExpectedCardVouchers,
            DeliveredCardVouchers = reconciliation.DeliveredCardVouchers,
            CardVouchersDifference = reconciliation.CardVouchersDifference,
            TotalDifference = reconciliation.TotalDifference,
            Message = message
        };
    }

    private static string BuildResultMessage(CashCutReconciliation r)
    {
        return r.Status switch
        {
            ReconciliationStatus.Balanced => "¡Corte Cuadrado! El efectivo y los váuchers coinciden con el sistema.",
            ReconciliationStatus.CashShortage => $"Conciliado con FALTANTE en efectivo de {Math.Abs(r.CashDifference):C2}.",
            ReconciliationStatus.CashSurplus => $"Conciliado con SOBRANTE en efectivo de {r.CashDifference:C2}.",
            ReconciliationStatus.VoucherShortage => $"Conciliado con FALTANTE en váuchers de tarjeta de {Math.Abs(r.CardVouchersDifference):C2}.",
            ReconciliationStatus.VoucherSurplus => $"Conciliado con SOBRANTE en váuchers de tarjeta de {r.CardVouchersDifference:C2}.",
            ReconciliationStatus.Discrepancy => $"Conciliado con DIFERENCIAS: Efectivo: {(r.CashDifference < 0 ? "-" : "+")}{Math.Abs(r.CashDifference):C2} | Tarjetas: {(r.CardVouchersDifference < 0 ? "-" : "+")}{Math.Abs(r.CardVouchersDifference):C2}.",
            _ => "Conciliación registrada."
        };
    }
}
