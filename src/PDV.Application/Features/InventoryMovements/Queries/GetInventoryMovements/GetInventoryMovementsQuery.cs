using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PDV.Application.Common.Interfaces;
using PDV.Application.Common.Models;
using PDV.Application.Features.InventoryMovements.Dtos;
using PDV.Domain.Enums;

namespace PDV.Application.Features.InventoryMovements.Queries.GetInventoryMovements;

public record GetInventoryMovementsQuery : IRequest<PaginatedList<InventoryMovementDto>>
{
    public int PageNumber { get; init; } = 1;
    public int PageSize { get; init; } = 10;
    public string? SearchQuery { get; init; }
    public Guid? BranchId { get; init; }
    public InventoryMovementType? Type { get; init; }
    public DateTime? StartDate { get; init; }
    public DateTime? EndDate { get; init; }
}

public class GetInventoryMovementsQueryHandler : IRequestHandler<GetInventoryMovementsQuery, PaginatedList<InventoryMovementDto>>
{
    private readonly IApplicationDbContext _context;

    public GetInventoryMovementsQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<PaginatedList<InventoryMovementDto>> Handle(GetInventoryMovementsQuery request, CancellationToken cancellationToken)
    {
        var query = _context.InventoryMovements
            .Include(x => x.Product)
            .Include(x => x.Branch)
            .AsNoTracking();

        if (request.BranchId.HasValue && request.BranchId.Value != Guid.Empty)
        {
            query = query.Where(x => x.BranchId == request.BranchId.Value);
        }

        if (request.Type.HasValue)
        {
            query = query.Where(x => x.Type == request.Type.Value);
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
            // Extender la fecha final al final del día (23:59:59)
            var endOfDay = endDate.Value.Date.AddDays(1).AddSeconds(-1);
            var endOfDayUtc = DateTime.SpecifyKind(endOfDay, DateTimeKind.Utc);
            query = query.Where(x => x.Date <= endOfDayUtc);
        }

        if (!string.IsNullOrWhiteSpace(request.SearchQuery))
        {
            var search = request.SearchQuery.Trim().ToLower();
            query = query.Where(x =>
                x.Product.Name.ToLower().Contains(search) ||
                x.Product.Code.ToLower().Contains(search) ||
                (x.Remarks != null && x.Remarks.ToLower().Contains(search))
            );
        }

        // Ordenar del más reciente al más antiguo
        query = query.OrderByDescending(x => x.Date);

        var projection = query.Select(x => new InventoryMovementDto
        {
            Id = x.Id,
            ProductId = x.ProductId,
            ProductName = x.Product.Name,
            ProductCode = x.Product.Code,
            BranchId = x.BranchId,
            BranchName = x.Branch.Name,
            Quantity = x.Quantity,
            Type = x.Type,
            Date = x.Date,
            ReferenceId = x.ReferenceId,
            Remarks = x.Remarks
        });

        return await PaginatedList<InventoryMovementDto>.CreateAsync(projection, request.PageNumber, request.PageSize, cancellationToken);
    }
}
