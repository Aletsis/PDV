using MediatR;
using Microsoft.EntityFrameworkCore;
using PDV.Application.Common.Interfaces;

namespace PDV.Application.Features.Outbox.Commands.MarkOutboxMessagesAsProcessing;

public record MarkOutboxMessagesAsProcessingCommand(List<Guid> MessageIds) : IRequest;

public class MarkOutboxMessagesAsProcessingCommandHandler : IRequestHandler<MarkOutboxMessagesAsProcessingCommand>
{
    private readonly IApplicationDbContext _context;

    public MarkOutboxMessagesAsProcessingCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task Handle(MarkOutboxMessagesAsProcessingCommand request, CancellationToken cancellationToken)
    {
        if (request.MessageIds == null || !request.MessageIds.Any())
            return;

        var messages = await _context.OutboxMessages
            .Where(x => request.MessageIds.Contains(x.Id))
            .ToListAsync(cancellationToken);

        foreach (var message in messages)
        {
            message.MarkAsProcessing();
        }

        await _context.SaveChangesAsync(cancellationToken);
    }
}
