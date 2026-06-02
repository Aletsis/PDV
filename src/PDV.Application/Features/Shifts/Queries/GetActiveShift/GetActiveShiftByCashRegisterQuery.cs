using MediatR;
using Microsoft.EntityFrameworkCore;
using PDV.Application.Common.Interfaces;
using PDV.Application.Features.Shifts.Dtos;
using PDV.Domain.Enums;

namespace PDV.Application.Features.Shifts.Queries.GetActiveShift;

/// <summary>
/// Retorna el turno activo (Status = Open) de la caja indicada.
/// Retorna null si no hay ninguno abierto.
/// </summary>
public record GetActiveShiftByCashRegisterQuery(Guid CashRegisterId) : IRequest<ShiftDto?>;

public class GetActiveShiftByCashRegisterQueryHandler
    : IRequestHandler<GetActiveShiftByCashRegisterQuery, ShiftDto?>
{
    private readonly IApplicationDbContext _context;

    public GetActiveShiftByCashRegisterQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<ShiftDto?> Handle(
        GetActiveShiftByCashRegisterQuery request,
        CancellationToken cancellationToken)
    {
        var shift = await _context.Shifts
            .AsNoTracking()
            .Include(s => s.CashRegister)
            .Where(s => s.CashRegisterId == request.CashRegisterId
                        && s.Status == ShiftStatus.Open)
            .OrderByDescending(s => s.StartTime)
            .FirstOrDefaultAsync(cancellationToken);

        if (shift is null) return null;

        return new ShiftDto
        {
            Id = shift.Id,
            CashRegisterId = shift.CashRegisterId,
            CashRegisterName = shift.CashRegister?.Name ?? string.Empty,
            UserId = shift.UserId,
            InitialCash = shift.InitialCash,
            StartTime = shift.StartTime,
            Status = shift.Status
        };
    }
}
