using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using PDV.Application.Common.Interfaces;
using PDV.WebUI.Hubs;

namespace PDV.WebUI.Services;

public class RealTimeSyncNotifier : IRealTimeSyncNotifier
{
    private readonly IHubContext<SyncHub> _hubContext;
    private readonly ILogger<RealTimeSyncNotifier> _logger;

    public RealTimeSyncNotifier(IHubContext<SyncHub> hubContext, ILogger<RealTimeSyncNotifier> logger)
    {
        _hubContext = hubContext;
        _logger = logger;
    }

    public async Task NotifyEntityChangedAsync(string entityName, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Broadcasting real-time sync notification for entity: {EntityName}", entityName);
            // Broadcast the notification to all connected clients (local cash registers)
            await _hubContext.Clients.All.SendAsync("ReceiveSyncNotification", entityName, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error broadcasting sync notification for entity: {EntityName}", entityName);
        }
    }
}
