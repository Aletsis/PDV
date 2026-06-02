using MediatR;
using PDV.Application.Common.Interfaces;

namespace PDV.Application.Features.Outbox.Commands.ProcessOutboxMessage;

public record ProcessOutboxMessageCommand(
    Guid Id,
    bool Success,
    string? ErrorMessage = null,
    int MaxAttempts = 5) : IRequest;

public class ProcessOutboxMessageCommandHandler : IRequestHandler<ProcessOutboxMessageCommand>
{
    private readonly IApplicationDbContext _context;

    public ProcessOutboxMessageCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task Handle(ProcessOutboxMessageCommand request, CancellationToken cancellationToken)
    {
        var message = await _context.OutboxMessages.FindAsync(new object[] { request.Id }, cancellationToken);
        if (message == null)
        {
            throw new KeyNotFoundException($"Mensaje de Outbox con ID '{request.Id}' no encontrado.");
        }

        if (request.Success)
        {
            message.MarkAsProcessed();
        }
        else
        {
            message.MarkAsFailed(request.ErrorMessage ?? "Error desconocido en el proceso de sincronización.", request.MaxAttempts);
        }

        await _context.SaveChangesAsync(cancellationToken);
    }
}
