using MediatR;
using Microsoft.EntityFrameworkCore;
using PDV.Application.Common.Interfaces;
using PDV.Domain.Entities;
using PDV.Domain.Enums;
using PDV.Domain.Exceptions;

namespace PDV.Application.Features.Shifts.Commands.OpenShift;

/// <summary>
/// Abre un nuevo turno para la caja indicada.
/// Lanza excepción si ya existe un turno abierto en esa caja.
/// </summary>
/// <param name="CashRegisterId">ID de la caja registradora.</param>
/// <param name="UserId">ID del usuario (cajero) que abre el turno.</param>
/// <param name="InitialCash">Fondo inicial de efectivo.</param>
public record OpenShiftCommand(
    Guid CashRegisterId,
    string UserId,
    decimal InitialCash) : IRequest<Guid>;

public class OpenShiftCommandHandler : IRequestHandler<OpenShiftCommand, Guid>
{
    private readonly IApplicationDbContext _context;

    public OpenShiftCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Guid> Handle(OpenShiftCommand request, CancellationToken cancellationToken)
    {
        // Validar que la caja exista y esté activa
        var cashRegister = await _context.CashRegisters
            .FirstOrDefaultAsync(c => c.Id == request.CashRegisterId && c.IsActive, cancellationToken);

        if (cashRegister is null)
            throw new DomainException("La caja registradora no existe o no está activa.");

        // Validar que no haya un turno ya abierto en esa caja
        var existingShift = await _context.Shifts
            .AsNoTracking()
            .AnyAsync(s => s.CashRegisterId == request.CashRegisterId
                           && s.Status == ShiftStatus.Open, cancellationToken);

        if (existingShift)
            throw new DomainException("Ya existe un turno abierto en esta caja. Debe cerrar el turno actual antes de abrir uno nuevo.");

        var shift = new Shift(request.CashRegisterId, request.UserId, request.InitialCash);

        _context.Shifts.Add(shift);
        await _context.SaveChangesAsync(cancellationToken);

        return shift.Id;
    }
}
