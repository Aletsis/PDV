using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using System;
using System.Threading.Tasks;

namespace PDV.WebUI.Hubs;

public class SyncHub : Hub
{
    private readonly ILogger<SyncHub> _logger;
    private readonly IConfiguration _configuration;

    public SyncHub(ILogger<SyncHub> logger, IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
    }

    public override async Task OnConnectedAsync()
    {
        var httpContext = Context.GetHttpContext();
        var clientApiKey = httpContext?.Request.Headers["X-Sync-Api-Key"].ToString();
        var serverApiKey = _configuration["SyncSettings:SyncApiKey"];

        if (!string.IsNullOrWhiteSpace(serverApiKey) && !string.Equals(clientApiKey, serverApiKey))
        {
            _logger.LogWarning("Unauthorized connection attempt to SyncHub from {Ip}", httpContext?.Connection.RemoteIpAddress);
            Context.Abort();
            return;
        }

        _logger.LogInformation("PDV Client connected to SyncHub. ConnectionId: {ConnectionId}", Context.ConnectionId);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _logger.LogInformation("PDV Client disconnected from SyncHub. ConnectionId: {ConnectionId}", Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }
}
