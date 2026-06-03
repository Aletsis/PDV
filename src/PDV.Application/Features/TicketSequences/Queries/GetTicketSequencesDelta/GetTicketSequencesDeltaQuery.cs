using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PDV.Application.Common.Interfaces;
using PDV.Domain.Enums;

namespace PDV.Application.Features.TicketSequences.Queries.GetTicketSequencesDelta;

public record GetTicketSequencesDeltaQuery(DateTime SinceUtc) : IRequest<List<TicketSequenceSyncDto>>;

public class TicketSequenceSyncDto
{
    public Guid Id { get; set; }
    public Guid CashRegisterId { get; set; }
    public TicketSequenceType SequenceType { get; set; }
    public int LastTicketNumber { get; set; }
    public string? Series { get; set; }
    public bool ResetOnNewShift { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? LastModifiedAt { get; set; }
}

public class GetTicketSequencesDeltaQueryHandler : IRequestHandler<GetTicketSequencesDeltaQuery, List<TicketSequenceSyncDto>>
{
    private readonly IApplicationDbContext _context;

    public GetTicketSequencesDeltaQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<TicketSequenceSyncDto>> Handle(GetTicketSequencesDeltaQuery request, CancellationToken cancellationToken)
    {
        var since = request.SinceUtc;

        return await _context.TicketSequences
            .IgnoreQueryFilters()
            .Where(t => t.CreatedAt > since || (t.LastModifiedAt != null && t.LastModifiedAt > since))
            .Select(t => new TicketSequenceSyncDto
            {
                Id = t.Id,
                CashRegisterId = t.CashRegisterId,
                SequenceType = t.SequenceType,
                LastTicketNumber = t.LastTicketNumber,
                Series = t.Series,
                ResetOnNewShift = t.ResetOnNewShift,
                IsDeleted = t.IsDeleted,
                CreatedAt = t.CreatedAt,
                LastModifiedAt = t.LastModifiedAt
            })
            .ToListAsync(cancellationToken);
    }
}
