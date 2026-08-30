using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PDV.Application.Common.Interfaces;
using PDV.Domain.Enums;

namespace PDV.Application.Features.InventoryMovements.Queries.GetInventoryStats;

public record GetInventoryStatsQuery(Guid? BranchId, DateTime? StartDate, DateTime? EndDate) : IRequest<InventoryStatsDto>;

public class InventoryStatsDto
{
    public int TotalMovements { get; set; }
    public int TotalPurchases { get; set; }
    public int TotalAdjustments { get; set; }
    public int TotalTransfers { get; set; }
}

public class GetInventoryStatsQueryHandler : IRequestHandler<GetInventoryStatsQuery, InventoryStatsDto>
{
    private readonly IApplicationDbContext _context;

    public GetInventoryStatsQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<InventoryStatsDto> Handle(GetInventoryStatsQuery request, CancellationToken cancellationToken)
    {
        var query = _context.InventoryMovements.AsNoTracking();

        if (request.BranchId.HasValue && request.BranchId.Value != Guid.Empty)
        {
            query = query.Where(x => x.BranchId == request.BranchId.Value);
        }

        if (request.StartDate.HasValue)
        {
            var start = request.StartDate.Value.Date;
            query = query.Where(x => x.Date >= start);
        }

        if (request.EndDate.HasValue)
        {
            var endOfDay = request.EndDate.Value.Date.AddDays(1).AddTicks(-1);
            query = query.Where(x => x.Date <= endOfDay);
        }

        var stats = new InventoryStatsDto
        {
            TotalMovements = await query.CountAsync(cancellationToken),
            TotalPurchases = await query.CountAsync(x => x.Type == InventoryMovementType.Purchase, cancellationToken),
            TotalAdjustments = await query.CountAsync(x => x.Type == InventoryMovementType.AdjustmentInput || x.Type == InventoryMovementType.AdjustmentOutput, cancellationToken),
            TotalTransfers = await query.CountAsync(x => x.Type == InventoryMovementType.Transfer, cancellationToken)
        };

        return stats;
    }
}
