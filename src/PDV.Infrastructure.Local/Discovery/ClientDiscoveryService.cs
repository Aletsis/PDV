using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace PDV.Infrastructure.Local.Discovery;

public class ClientDiscoveryService : IClientDiscoveryService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<ClientDiscoveryService> _logger;
    
    private string? _cachedServerUrl;
    private readonly object _lock = new();

    public ClientDiscoveryService(IConfiguration configuration, ILogger<ClientDiscoveryService> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<string?> GetServerUrlAsync(CancellationToken cancellationToken)
    {
        // 1. Check in-memory cache
        lock (_lock)
        {
            if (!string.IsNullOrWhiteSpace(_cachedServerUrl))
            {
                return _cachedServerUrl;
            }
        }

        // 2. Level 1: Static DNS / Configuration
        var configUrl = _configuration.GetValue<string>("SyncSettings:ServerBaseUrl");
        
        // If static URL is configured and is not a special auto-discovery keyword, use it directly (Level 1)
        if (!string.IsNullOrWhiteSpace(configUrl) && 
            !configUrl.Equals("auto", StringComparison.OrdinalIgnoreCase) && 
            !configUrl.Equals("discover", StringComparison.OrdinalIgnoreCase))
        {
            var cleanedUrl = configUrl.Trim().TrimEnd('/');
            _logger.LogInformation("Using configured Level 1 static/DNS server URL: {Url}", cleanedUrl);
            
            lock (_lock)
            {
                _cachedServerUrl = cleanedUrl;
            }
            return cleanedUrl;
        }

        // 3. Level 2: Local UDP Auto-Discovery
        _logger.LogInformation("Server URL not configured or set to auto-discovery. Initiating Level 2 Local Network Discovery...");
        
        var discoveredUrl = await DiscoverServerAsync(cancellationToken);
        if (!string.IsNullOrWhiteSpace(discoveredUrl))
        {
            var cleanedUrl = discoveredUrl.Trim().TrimEnd('/');
            lock (_lock)
            {
                _cachedServerUrl = cleanedUrl;
            }
            return cleanedUrl;
        }

        return null;
    }

    public void InvalidateUrl()
    {
        lock (_lock)
        {
            if (_cachedServerUrl != null)
            {
                _logger.LogWarning("Invalidating cached server URL: {Url}. Discovery will run on next request.", _cachedServerUrl);
                _cachedServerUrl = null;
            }
        }
    }

    private async Task<string?> DiscoverServerAsync(CancellationToken cancellationToken)
    {
        var port = _configuration.GetValue<int>("SyncSettings:DiscoveryPort", 38120);
        var timeoutMs = _configuration.GetValue<int>("SyncSettings:DiscoveryTimeoutMs", 3000);
        var maxAttempts = _configuration.GetValue<int>("SyncSettings:DiscoveryMaxAttempts", 3);

        _logger.LogInformation("Starting UDP broadcast discovery on port {Port}...", port);
        var requestBytes = Encoding.UTF8.GetBytes("PDV_DISCOVER_REQUEST");

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            if (cancellationToken.IsCancellationRequested) break;

            _logger.LogInformation("Sending discovery broadcast attempt {Attempt}/{MaxAttempts}...", attempt, maxAttempts);

            // Find all active IPv4 unicast addresses to send broadcasts from each interface to bypass routing table limitations
            var interfaces = NetworkInterface.GetAllNetworkInterfaces()
                .Where(ni => ni.OperationalStatus == OperationalStatus.Up && 
                             ni.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                .ToList();

            foreach (var ni in interfaces)
            {
                var ipProps = ni.GetIPProperties();
                foreach (var unicast in ipProps.UnicastAddresses)
                {
                    if (unicast.Address.AddressFamily != AddressFamily.InterNetwork) continue;

                    try
                    {
                        using var client = new UdpClient();
                        client.EnableBroadcast = true;
                        
                        // Bind to this interface's IP and ephemeral port
                        client.Client.Bind(new IPEndPoint(unicast.Address, 0));

                        // Broadcast endpoint
                        var broadcastEndpoint = new IPEndPoint(IPAddress.Broadcast, port);
                        await client.SendAsync(requestBytes, requestBytes.Length, broadcastEndpoint);
                        
                        // Listen for response on this socket
                        var receiveTask = client.ReceiveAsync(cancellationToken).AsTask();
                        var delayTask = Task.Delay(timeoutMs, cancellationToken);

                        var completedTask = await Task.WhenAny(receiveTask, delayTask);
                        if (completedTask == receiveTask)
                        {
                            var result = await receiveTask;
                            var response = Encoding.UTF8.GetString(result.Buffer);
                            if (response.StartsWith("PDV_DISCOVER_RESPONSE|"))
                            {
                                var serverUrl = response.Substring("PDV_DISCOVER_RESPONSE|".Length);
                                _logger.LogInformation("Discovered server URL: {Url} (from {Sender} via interface {Interface})", 
                                    serverUrl, result.RemoteEndPoint, ni.Name);
                                return serverUrl;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        // Log debug level since some interfaces (like virtual ones) might fail to send
                        _logger.LogDebug(ex, "Failed sending discovery broadcast on interface {Interface} ({IP})", ni.Name, unicast.Address);
                    }
                }
            }

            // Fallback: Default broadcast without binding to a specific interface
            try
            {
                using var client = new UdpClient();
                client.EnableBroadcast = true;
                client.Client.Bind(new IPEndPoint(IPAddress.Any, 0));

                var broadcastEndpoint = new IPEndPoint(IPAddress.Broadcast, port);
                await client.SendAsync(requestBytes, requestBytes.Length, broadcastEndpoint);

                var receiveTask = client.ReceiveAsync(cancellationToken).AsTask();
                var delayTask = Task.Delay(timeoutMs, cancellationToken);

                var completedTask = await Task.WhenAny(receiveTask, delayTask);
                if (completedTask == receiveTask)
                {
                    var result = await receiveTask;
                    var response = Encoding.UTF8.GetString(result.Buffer);
                    if (response.StartsWith("PDV_DISCOVER_RESPONSE|"))
                    {
                        var serverUrl = response.Substring("PDV_DISCOVER_RESPONSE|".Length);
                        _logger.LogInformation("Discovered server URL via default interface: {Url} (from {Sender})", serverUrl, result.RemoteEndPoint);
                        return serverUrl;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Default interface broadcast attempt {Attempt} failed.", attempt);
            }

            _logger.LogWarning("Discovery attempt {Attempt} did not receive any response within {Timeout}ms.", attempt, timeoutMs);
        }

        _logger.LogError("Server discovery failed. No response received after {MaxAttempts} attempts.", maxAttempts);
        return null;
    }
}
