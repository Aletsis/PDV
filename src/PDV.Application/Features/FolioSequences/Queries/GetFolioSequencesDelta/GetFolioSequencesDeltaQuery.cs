using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PDV.Application.Common.Interfaces;
using PDV.Domain.Enums;

namespace PDV.Application.Features.FolioSequences.Queries.GetFolioSequencesDelta;

public record GetFolioSequencesDeltaQuery(DateTime SinceUtc) : IRequest<List<FolioSequenceSyncDto>>;

public class FolioSequenceSyncDto
{
    public Guid Id { get; set; }
    public Guid BranchId { get; set; }
    public InvoiceType SeriesType { get; set; }
    public string Series { get; set; } = string.Empty;
    public string? ConceptCode { get; set; }
    public int LastFolio { get; set; }
    public int FolioDigits { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? LastModifiedAt { get; set; }
}

public class GetFolioSequencesDeltaQueryHandler : IRequestHandler<GetFolioSequencesDeltaQuery, List<FolioSequenceSyncDto>>
{
    private readonly IApplicationDbContext _context;

    public GetFolioSequencesDeltaQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<FolioSequenceSyncDto>> Handle(GetFolioSequencesDeltaQuery request, CancellationToken cancellationToken)
    {
        var since = request.SinceUtc;

        return await _context.FolioSequences
            .IgnoreQueryFilters()
            .Where(f => f.CreatedAt > since || (f.LastModifiedAt != null && f.LastModifiedAt > since))
            .Select(f => new FolioSequenceSyncDto
            {
                Id = f.Id,
                BranchId = f.BranchId,
                SeriesType = f.SeriesType,
                Series = f.Series,
                ConceptCode = f.ConceptCode,
                LastFolio = f.LastFolio,
                FolioDigits = f.FolioDigits,
                IsDeleted = f.IsDeleted,
                CreatedAt = f.CreatedAt,
                LastModifiedAt = f.LastModifiedAt
            })
            .ToListAsync(cancellationToken);
    }
}
