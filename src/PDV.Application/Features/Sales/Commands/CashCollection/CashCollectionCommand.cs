using MediatR;
using Microsoft.EntityFrameworkCore;
using PDV.Application.Common.Interfaces;
using PDV.Domain.Entities;
using PDV.Domain.ValueObjects;
using PDV.Domain.Enums;

namespace PDV.Application.Features.Sales.Commands.CashCollection;

public record CashCollectionCommand(Guid CashRegisterId, Guid CashierId, decimal Amount, string Reason) : IRequest<Guid>;

public class CashCollectionCommandHandler : IRequestHandler<CashCollectionCommand, Guid>
{
    private readonly IApplicationDbContext _context;

    public CashCollectionCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Guid> Handle(CashCollectionCommand request, CancellationToken cancellationToken)
    {
        var activeShift = await _context.Shifts
            .FirstOrDefaultAsync(s => s.CashRegisterId == request.CashRegisterId && s.Status == ShiftStatus.Open, cancellationToken);

        if (activeShift == null)
        {
            throw new InvalidOperationException("No hay un turno activo para la caja registradora indicada.");
        }

        var denominations = DecomposeAmount(request.Amount);

        // Usar constructor de dominio
        var col = new PDV.Domain.Entities.CashCollection(
            shiftId: activeShift.Id,
            cashRegisterId: request.CashRegisterId,
            userId: request.CashierId != Guid.Empty ? request.CashierId.ToString() : activeShift.UserId,
            denominations: denominations,
            reason: request.Reason,
            employeeId: request.CashierId == Guid.Empty ? null : request.CashierId);

        _context.CashCollections.Add(col);
        await _context.SaveChangesAsync(cancellationToken);

        return col.Id;
    }

    private List<CashDenomination> DecomposeAmount(decimal amount)
    {
        var result = new List<CashDenomination>();
        var remaining = amount;

        if (remaining >= 1000m)
        {
            int qty = (int)(remaining / 1000m);
            result.Add(new CashDenomination(DenominationType.Bill_1000, qty));
            remaining %= 1000m;
        }
        if (remaining >= 500m)
        {
            int qty = (int)(remaining / 500m);
            result.Add(new CashDenomination(DenominationType.Bill_500, qty));
            remaining %= 500m;
        }
        if (remaining >= 200m)
        {
            int qty = (int)(remaining / 200m);
            result.Add(new CashDenomination(DenominationType.Bill_200, qty));
            remaining %= 200m;
        }
        if (remaining >= 100m)
        {
            int qty = (int)(remaining / 100m);
            result.Add(new CashDenomination(DenominationType.Bill_100, qty));
            remaining %= 100m;
        }
        if (remaining >= 50m)
        {
            int qty = (int)(remaining / 50m);
            result.Add(new CashDenomination(DenominationType.Bill_50, qty));
            remaining %= 50m;
        }
        if (remaining >= 20m)
        {
            int qty = (int)(remaining / 20m);
            result.Add(new CashDenomination(DenominationType.Bill_20, qty));
            remaining %= 20m;
        }
        if (remaining >= 10m)
        {
            int qty = (int)(remaining / 10m);
            result.Add(new CashDenomination(DenominationType.Coin_10, qty));
            remaining %= 10m;
        }
        if (remaining >= 5m)
        {
            int qty = (int)(remaining / 5m);
            result.Add(new CashDenomination(DenominationType.Coin_5, qty));
            remaining %= 5m;
        }
        if (remaining >= 2m)
        {
            int qty = (int)(remaining / 2m);
            result.Add(new CashDenomination(DenominationType.Coin_2, qty));
            remaining %= 2m;
        }
        if (remaining >= 1m)
        {
            int qty = (int)(remaining / 1m);
            result.Add(new CashDenomination(DenominationType.Coin_1, qty));
            remaining %= 1m;
        }
        if (remaining > 0)
        {
            int qty = (int)Math.Round(remaining / 0.10m);
            if (qty > 0)
            {
                result.Add(new CashDenomination(DenominationType.Coin_020, qty));
            }
        }

        return result;
    }
}
