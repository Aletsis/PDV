using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PDV.Application.Common.Interfaces;
using PDV.Application.Features.Sync.Commands;
using PDV.Application.Features.Sync.Dtos;
using PDV.Domain.Entities;
using PDV.Domain.Enums;

namespace PDV.Infrastructure.Server.BackgroundServices;

/// <summary>
/// Background service that processes replication/synchronization messages stored in the server's Inbox.
/// This processes messages asynchronously, guaranteeing order, durability, and idempotency.
/// </summary>
public class InboxWorker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<InboxWorker> _logger;

    public InboxWorker(
        IServiceProvider serviceProvider,
        ILogger<InboxWorker> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Starting InboxWorker...");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessPendingMessagesAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing inbox replication messages.");
            }

            // Check every 5 seconds for new sync events in the inbox
            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
        }

        _logger.LogInformation("InboxWorker stopped.");
    }

    private async Task ProcessPendingMessagesAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var mediator = scope.ServiceProvider.GetRequiredService<ISender>();

        // Query pending messages ordered by ReceivedAt to preserve chronology
        var pendingMessages = await context.InboxMessages
            .Where(m => m.State == InboxState.Pending)
            .OrderBy(m => m.ReceivedAt)
            .Take(50) // Process in small batches to manage resource consumption
            .ToListAsync(cancellationToken);

        if (pendingMessages.Count == 0) return;

        _logger.LogInformation("Processing {Count} pending messages in server inbox...", pendingMessages.Count);

        foreach (var message in pendingMessages)
        {
            message.MarkAsProcessing();
            await context.SaveChangesAsync(cancellationToken);

            try
            {
                var dto = new OutboxSyncDto(message.MessageId, message.EventType, message.Payload, message.ReceivedAt);
                var command = new ProcessSyncEventCommand(dto);

                var result = await mediator.Send(command, cancellationToken);

                if (result.Success)
                {
                    message.MarkAsProcessed();
                    _logger.LogInformation("Successfully processed sync message {MessageId} ({EventType}).", message.MessageId, message.EventType);
                }
                else
                {
                    message.MarkAsFailed(result.ErrorMessage ?? "Unknown failure", maxAttempts: 5);
                    _logger.LogWarning("Failed to process sync message {MessageId} ({EventType}). Error: {Error}. Attempt: {Attempts}",
                        message.MessageId, message.EventType, result.ErrorMessage, message.Attempts);
                }
            }
            catch (Exception ex)
            {
                var errorMsg = ex.Message;
                var inner = ex.InnerException;
                while (inner != null)
                {
                    errorMsg += " ---> " + inner.Message;
                    inner = inner.InnerException;
                }

                message.MarkAsFailed(errorMsg, maxAttempts: 5);
                _logger.LogError(ex, "Exception thrown while processing sync message {MessageId} ({EventType}). Attempt: {Attempts}",
                    message.MessageId, message.EventType, message.Attempts);
            }

            await context.SaveChangesAsync(cancellationToken);
        }
    }
}
