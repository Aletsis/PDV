using MediatR;
using Microsoft.EntityFrameworkCore;
using PDV.Application.Common.Interfaces;
using PDV.Domain.Entities;
using PDV.Domain.Enums;
using PDV.Domain.Exceptions;

namespace PDV.Application.Features.CashRegisters.Commands.CreateCashRegister;

public record CreateCashRegisterCommand(
    string Name,
    string Location,
    Guid BranchId,
    CashRegisterMode Mode = CashRegisterMode.SalesFloor,
    string? AssignedUserId = null,
    Guid? AssignedPrinterId = null,
    string? IpAddress = null
) : IRequest<Guid>;

public class CreateCashRegisterCommandHandler : IRequestHandler<CreateCashRegisterCommand, Guid>
{
    private readonly IApplicationDbContext _context;

    public CreateCashRegisterCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Guid> Handle(CreateCashRegisterCommand request, CancellationToken cancellationToken)
    {
        // ── Unicidad de IP (validación de aplicación antes de persistir) ──────
        if (!string.IsNullOrWhiteSpace(request.IpAddress))
        {
            var ipNormalized = request.IpAddress.Trim();
            var ipTaken = await _context.CashRegisters
                .AnyAsync(c => c.IpAddress == ipNormalized, cancellationToken);

            if (ipTaken)
                throw new DomainException($"La IP '{ipNormalized}' ya está asignada a otra caja registradora.");
        }

        // ── Creación del agregado ─────────────────────────────────────────────
        var entity = new CashRegister(request.Name, request.Location, request.BranchId, request.Mode);

        if (!string.IsNullOrEmpty(request.AssignedUserId))
            entity.AssignUser(request.AssignedUserId);

        if (request.AssignedPrinterId.HasValue)
            entity.AssignPrinter(request.AssignedPrinterId.Value);

        if (!string.IsNullOrWhiteSpace(request.IpAddress))
            entity.BindToIp(request.IpAddress);

        _context.CashRegisters.Add(entity);
        await _context.SaveChangesAsync(cancellationToken);
        return entity.Id;
    }
}

