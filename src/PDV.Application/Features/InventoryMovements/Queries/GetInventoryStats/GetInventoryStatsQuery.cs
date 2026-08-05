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

        var startDate = request.StartDate;
        if (startDate.HasValue)
        {
            if (startDate.Value.Kind == DateTimeKind.Local)
                startDate = startDate.Value.ToUniversalTime();
            else if (startDate.Value.Kind == DateTimeKind.Unspecified)
                startDate = DateTime.SpecifyKind(startDate.Value, DateTimeKind.Utc);
        }

        var endDate = request.EndDate;
        if (endDate.HasValue)
        {
            if (endDate.Value.Kind == DateTimeKind.Local)
                endDate = endDate.Value.ToUniversalTime();
            else if (endDate.Value.Kind == DateTimeKind.Unspecified)
                endDate = DateTime.SpecifyKind(endDate.Value, DateTimeKind.Utc);
        }

        if (startDate.HasValue)
        {
            query = query.Where(x => x.Date >= startDate.Value);
        }

        if (endDate.HasValue)
        {
            var endOfDay = endDate.Value.Date.AddDays(1).AddSeconds(-1);
            var endOfDayUtc = DateTime.SpecifyKind(endOfDay, DateTimeKind.Utc);
            query = query.Where(x => x.Date <= endOfDayUtc);
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
