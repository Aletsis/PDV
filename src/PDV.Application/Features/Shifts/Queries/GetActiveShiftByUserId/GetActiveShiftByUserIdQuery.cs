using MediatR;
using Microsoft.EntityFrameworkCore;
using PDV.Application.Common.Interfaces;
using PDV.Application.Features.Shifts.Dtos;
using PDV.Domain.Enums;

namespace PDV.Application.Features.Shifts.Queries.GetActiveShiftByUserId;

/// <summary>
/// Retorna el turno activo (Status = Open) del usuario indicado.
/// Retorna null si no hay ninguno abierto.
/// </summary>
public record GetActiveShiftByUserIdQuery(string UserId) : IRequest<ShiftDto?>;

public class GetActiveShiftByUserIdQueryHandler
    : IRequestHandler<GetActiveShiftByUserIdQuery, ShiftDto?>
{
    private readonly IApplicationDbContext _context;

    public GetActiveShiftByUserIdQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<ShiftDto?> Handle(
        GetActiveShiftByUserIdQuery request,
        CancellationToken cancellationToken)
    {
        var shift = await _context.Shifts
            .AsNoTracking()
            .Include(s => s.CashRegister)
            .Where(s => s.UserId == request.UserId
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
