using MediatR;
using Microsoft.EntityFrameworkCore;
using PDV.Application.Common.Interfaces;
using PDV.Domain.Exceptions;

namespace PDV.Application.Features.CashRegisters.Commands.UpdateCashRegister;

public record UpdateCashRegisterCommand(
    Guid Id,
    string Name,
    string Location,
    Guid BranchId,
    Guid? AssignedEmployeeId,
    Guid? AssignedPrinterId,
    bool IsActive,
    string? IpAddress = null
) : IRequest<bool>;

public class UpdateCashRegisterCommandHandler : IRequestHandler<UpdateCashRegisterCommand, bool>
{
    private readonly IApplicationDbContext _context;

    public UpdateCashRegisterCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<bool> Handle(UpdateCashRegisterCommand request, CancellationToken cancellationToken)
    {
        var entity = await _context.CashRegisters.FindAsync(new object[] { request.Id }, cancellationToken);
        if (entity == null) return false;

        // ── Unicidad de IP (excluye la propia caja) ──────────────────────────
        if (!string.IsNullOrWhiteSpace(request.IpAddress))
        {
            var ipNormalized = request.IpAddress.Trim();
            var ipTaken = await _context.CashRegisters
                .AnyAsync(c => c.IpAddress == ipNormalized && c.Id != request.Id, cancellationToken);

            if (ipTaken)
                throw new DomainException($"La IP '{ipNormalized}' ya está asignada a otra caja registradora.");
        }

        // ── Actualización del agregado ────────────────────────────────────────
        entity.Update(request.Name, request.Location);
        entity.AssignEmployee(request.AssignedEmployeeId);
        entity.AssignPrinter(request.AssignedPrinterId);
        entity.BindToIp(string.IsNullOrWhiteSpace(request.IpAddress) ? null : request.IpAddress);

        if (request.IsActive && !entity.IsActive)
            entity.Activate();
        else if (!request.IsActive && entity.IsActive)
            entity.Deactivate();

        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }
}

