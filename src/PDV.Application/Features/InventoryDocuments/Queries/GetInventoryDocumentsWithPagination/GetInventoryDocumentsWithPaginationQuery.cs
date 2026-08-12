using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PDV.Application.Common.Interfaces;
using PDV.Application.Common.Models;
using PDV.Application.Features.InventoryDocuments.Dtos;
using PDV.Domain.Enums;

namespace PDV.Application.Features.InventoryDocuments.Queries.GetInventoryDocumentsWithPagination;

public record GetInventoryDocumentsWithPaginationQuery : IRequest<PaginatedList<InventoryDocumentDto>>
{
    public int PageNumber { get; init; } = 1;
    public int PageSize { get; init; } = 15;
    public string? SearchQuery { get; init; }
    public Guid? BranchId { get; init; }
    public InventoryMovementType? Type { get; init; }
    public InventoryMovementSubtype? Subtype { get; init; }
    public OutboxState? SyncStatus { get; init; }
    public DateTime? StartDate { get; init; }
    public DateTime? EndDate { get; init; }
}

public class GetInventoryDocumentsWithPaginationQueryHandler : IRequestHandler<GetInventoryDocumentsWithPaginationQuery, PaginatedList<InventoryDocumentDto>>
{
    private readonly IApplicationDbContext _context;

    public GetInventoryDocumentsWithPaginationQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<PaginatedList<InventoryDocumentDto>> Handle(GetInventoryDocumentsWithPaginationQuery request, CancellationToken cancellationToken)
    {
        var query = _context.InventoryDocuments
            .Include(d => d.Branch)
            .Include(d => d.DestinationBranch)
            .Include(d => d.Items)
                .ThenInclude(i => i.Product)
            .AsNoTracking();

        if (request.BranchId.HasValue && request.BranchId.Value != Guid.Empty)
        {
            query = query.Where(d => d.BranchId == request.BranchId.Value || d.DestinationBranchId == request.BranchId.Value);
        }

        if (request.Type.HasValue)
        {
            query = query.Where(d => d.Type == request.Type.Value);
        }

        if (request.Subtype.HasValue)
        {
            query = query.Where(d => d.Subtype == request.Subtype.Value);
        }

        if (request.SyncStatus.HasValue)
        {
            query = query.Where(d => d.SyncStatus == request.SyncStatus.Value);
        }

        if (request.StartDate.HasValue)
        {
            var startUtc = request.StartDate.Value.Date.ToUniversalTime();
            query = query.Where(d => d.Date >= startUtc);
        }

        if (request.EndDate.HasValue)
        {
            var endUtc = request.EndDate.Value.Date.AddDays(1).AddTicks(-1).ToUniversalTime();
            query = query.Where(d => d.Date <= endUtc);
        }

        if (!string.IsNullOrWhiteSpace(request.SearchQuery))
        {
            var term = request.SearchQuery.Trim().ToLower();
            query = query.Where(d => d.Folio.ToLower().Contains(term) ||
                                     d.Series.ToLower().Contains(term) ||
                                     (d.SupplierName != null && d.SupplierName.ToLower().Contains(term)) ||
                                     (d.SupplierCode != null && d.SupplierCode.ToLower().Contains(term)) ||
                                     (d.Reference != null && d.Reference.ToLower().Contains(term)) ||
                                     (d.Remarks != null && d.Remarks.ToLower().Contains(term)) ||
                                     d.CreatedBy.ToLower().Contains(term));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(d => d.Date)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(d => new InventoryDocumentDto
            {
                Id = d.Id,
                BranchId = d.BranchId,
                BranchName = d.Branch.Name,
                DestinationBranchId = d.DestinationBranchId,
                DestinationBranchName = d.DestinationBranch != null ? d.DestinationBranch.Name : null,
                Type = d.Type,
                Subtype = d.Subtype,
                Series = d.Series,
                Folio = d.Folio,
                SupplierId = d.SupplierId,
                SupplierCode = d.SupplierCode,
                SupplierName = d.SupplierName,
                Reference = d.Reference,
                Remarks = d.Remarks,
                CreatedBy = d.CreatedBy,
                Date = d.Date,
                SyncStatus = d.SyncStatus,
                Attempts = d.Attempts,
                LastAttemptAt = d.LastAttemptAt,
                SyncErrorMessage = d.SyncErrorMessage,
                ExternalDocumentId = d.ExternalDocumentId,
                ExternalSeries = d.ExternalSeries,
                ExternalFolio = d.ExternalFolio,
                TotalUnits = d.Items.Sum(i => i.Quantity),
                TotalAmount = d.Items.Sum(i => i.Quantity * i.UnitCost),
                Items = d.Items.Select(i => new InventoryDocumentItemDto
                {
                    Id = i.Id,
                    ProductId = i.ProductId,
                    ProductCode = i.Product.Code,
                    ProductName = i.Product.Name,
                    Quantity = i.Quantity,
                    UnitCost = i.UnitCost,
                    Remarks = i.Remarks
                }).ToList()
            })
            .ToListAsync(cancellationToken);

        return new PaginatedList<InventoryDocumentDto>(items, totalCount, request.PageNumber, request.PageSize);
    }
}
