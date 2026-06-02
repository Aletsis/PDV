using MediatR;
using Microsoft.EntityFrameworkCore;
using PDV.Application.Common.Interfaces;
using PDV.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace PDV.Application.Features.Sales.Queries.GetClosedCuts;

public record GetClosedCutsQuery(DateTime? StartDate = null, DateTime? EndDate = null) : IRequest<List<ClosedCutDto>>;

public class ClosedCutDto
{
    public Guid ShiftId { get; set; }
    public string CashRegisterName { get; set; } = string.Empty;
    public string CashierName { get; set; } = string.Empty;
    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public decimal TotalSales { get; set; }
    public int PendingSalesCount { get; set; }
    public decimal PendingSalesTotal { get; set; }
    public bool IsGlobalInvoiced { get; set; }
    public string? GlobalInvoiceNumber { get; set; }
    public string? GlobalInvoiceUuid { get; set; }
    public List<ClosedCutReturnDto> Returns { get; set; } = new();
}

public class ClosedCutReturnDto
{
    public Guid ReturnId { get; set; }
    public DateTime ReturnDate { get; set; }
    public decimal TotalRefund { get; set; }
    public string ClientName { get; set; } = string.Empty;
    public bool HasCreditNote { get; set; }
    public string? CreditNoteNumber { get; set; }
    public string? CreditNoteUuid { get; set; }
    public bool CanGenerateCreditNote { get; set; }
}

public class GetClosedCutsQueryHandler : IRequestHandler<GetClosedCutsQuery, List<ClosedCutDto>>
{
    private readonly IApplicationDbContext _context;

    public GetClosedCutsQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<ClosedCutDto>> Handle(GetClosedCutsQuery request, CancellationToken cancellationToken)
    {
        var shiftsQuery = _context.Shifts
            .Include(s => s.CashRegister)
            .Where(s => s.Status == ShiftStatus.Closed);

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

        var shifts = await shiftsQuery
            .OrderByDescending(s => s.EndTime)
            .ToListAsync(cancellationToken);

        var result = new List<ClosedCutDto>();

        foreach (var shift in shifts)
        {
            // Ventas pagadas y no canceladas asociadas al turno
            var sales = await _context.Sales
                .Where(s => s.ShiftId == shift.Id && s.IsPaid && !s.IsCancelled)
                .ToListAsync(cancellationToken);

            var totalSales = sales.Sum(s => s.TotalAmount);
            
            // Ventas pendientes de facturar (no facturadas individual ni globalmente)
            var pendingSales = sales.Where(s => !s.IsInvoiced && s.InvoiceId == null).ToList();
            var pendingSalesCount = pendingSales.Count;
            var pendingSalesTotal = pendingSales.Sum(s => s.TotalAmount);

            // Obtener factura global si existe
            string? globalInvoiceNum = null;
            string? globalInvoiceUuid = null;
            if (shift.IsGlobalInvoiced && !string.IsNullOrEmpty(shift.GlobalInvoiceId))
            {
                if (Guid.TryParse(shift.GlobalInvoiceId, out var globalInvoiceGuid))
                {
                    var globalInvoice = await _context.Invoices
                        .FirstOrDefaultAsync(i => i.Id == globalInvoiceGuid, cancellationToken);
                    if (globalInvoice != null)
                    {
                        globalInvoiceNum = globalInvoice.InvoiceNumber;
                        globalInvoiceUuid = globalInvoice.Uuid;
                    }
                }
            }

            // Obtener devoluciones asociadas al turno
            var returns = await _context.Returns
                .Include(r => r.Client)
                .Where(r => r.ShiftId == shift.Id && r.IsCompleted)
                .ToListAsync(cancellationToken);

            var returnDtos = new List<ClosedCutReturnDto>();
            foreach (var ret in returns)
            {
                // Buscar si existe nota de crédito para esta devolución
                var creditNote = await _context.Invoices
                    .FirstOrDefaultAsync(i => i.ReturnId == ret.Id && i.Type == InvoiceType.CreditNote, cancellationToken);

                bool hasCreditNote = creditNote != null;
                string? creditNoteNum = creditNote?.InvoiceNumber;
                string? creditNoteUuid = creditNote?.Uuid;

                // Se puede facturar la nota de crédito si la venta original existe y estaba facturada (individual o globalmente)
                bool canGenerate = false;
                if (ret.SaleId.HasValue)
                {
                    var origSale = await _context.Sales
                        .FirstOrDefaultAsync(s => s.Id == ret.SaleId.Value, cancellationToken);
                    if (origSale != null && (origSale.IsInvoiced || origSale.InvoiceId != null))
                    {
                        canGenerate = true;
                    }
                }

                returnDtos.Add(new ClosedCutReturnDto
                {
                    ReturnId = ret.Id,
                    ReturnDate = ret.ReturnDate,
                    TotalRefund = ret.TotalRefund,
                    ClientName = ret.Client?.Name ?? "Público General",
                    HasCreditNote = hasCreditNote,
                    CreditNoteNumber = creditNoteNum,
                    CreditNoteUuid = creditNoteUuid,
                    CanGenerateCreditNote = canGenerate
                });
            }

            // Obtener el nombre del cajero
            var cashierName = "Desconocido";
            if (Guid.TryParse(shift.UserId, out var cashierGuid))
            {
                var employee = await _context.Employees
                    .FirstOrDefaultAsync(e => e.Id == cashierGuid, cancellationToken);
                if (employee != null)
                {
                    cashierName = employee.Name;
                }
            }
            else
            {
                cashierName = shift.UserId;
            }

            result.Add(new ClosedCutDto
            {
                ShiftId = shift.Id,
                CashRegisterName = shift.CashRegister?.Name ?? "Caja",
                CashierName = cashierName,
                StartTime = shift.StartTime,
                EndTime = shift.EndTime,
                TotalSales = totalSales,
                PendingSalesCount = pendingSalesCount,
                PendingSalesTotal = pendingSalesTotal,
                IsGlobalInvoiced = shift.IsGlobalInvoiced,
                GlobalInvoiceNumber = globalInvoiceNum,
                GlobalInvoiceUuid = globalInvoiceUuid,
                Returns = returnDtos
            });
        }

        return result;
    }
}
