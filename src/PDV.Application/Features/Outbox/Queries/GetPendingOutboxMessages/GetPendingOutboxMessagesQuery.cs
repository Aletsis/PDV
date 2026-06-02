using MediatR;
using Microsoft.EntityFrameworkCore;
using PDV.Application.Common.Interfaces;
using PDV.Application.Features.Outbox.Dtos;
using PDV.Domain.Enums;

namespace PDV.Application.Features.Outbox.Queries.GetPendingOutboxMessages;

public record GetPendingOutboxMessagesQuery(int Limit = 50) : IRequest<List<OutboxMessageDto>>;

public class GetPendingOutboxMessagesQueryHandler : IRequestHandler<GetPendingOutboxMessagesQuery, List<OutboxMessageDto>>
{
    private readonly IApplicationDbContext _context;

    public GetPendingOutboxMessagesQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<OutboxMessageDto>> Handle(GetPendingOutboxMessagesQuery request, CancellationToken cancellationToken)
    {
        return await _context.OutboxMessages
            .Where(x => x.State == OutboxState.Pending)
            .OrderBy(x => x.CreatedAt)
            .Take(request.Limit)
            .Select(x => new OutboxMessageDto
            {
                Id = x.Id,
                EventType = x.EventType,
                Payload = x.Payload,
                CreatedAt = x.CreatedAt,
                State = x.State,
                Attempts = x.Attempts,
                LastAttemptAt = x.LastAttemptAt,
                ErrorMessage = x.ErrorMessage
            })
            .ToListAsync(cancellationToken);
    }
}
