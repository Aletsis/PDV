using System;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace PDV.Infrastructure.Server.Discovery;

/// <summary>
/// Hosted service that listens on a UDP port for discovery requests from PDV clients (cajas)
/// and responds with the server's base URL. Supports Zero-Configuration.
/// </summary>
public class ServerDiscoveryHostedService : BackgroundService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<ServerDiscoveryHostedService> _logger;

    public ServerDiscoveryHostedService(IConfiguration configuration, ILogger<ServerDiscoveryHostedService> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var port = _configuration.GetValue<int>("SyncSettings:DiscoveryPort", 38120);
        _logger.LogInformation("Starting Server UDP Discovery Listener on port {Port}...", port);

        UdpClient? listener = null;
        var retryCount = 0;
        
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                listener = new UdpClient();
                listener.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                listener.Client.Bind(new IPEndPoint(IPAddress.Any, port));
                
                _logger.LogInformation("Server UDP Discovery Listener successfully bound to port {Port}.", port);
                retryCount = 0; // Reset retry count upon success

                while (!stoppingToken.IsCancellationRequested)
                {
                    var receiveResult = await listener.ReceiveAsync(stoppingToken);
                    var requestMessage = Encoding.UTF8.GetString(receiveResult.Buffer);

                    if (requestMessage == "PDV_DISCOVER_REQUEST")
                    {
                        _logger.LogInformation("Received discovery request from client: {ClientEndpoint}", receiveResult.RemoteEndPoint);

                        var serverUrl = ResolveServerBaseUrl();
                        _logger.LogInformation("Resolved server base URL to announce: {Url}", serverUrl);

                        var responseMessage = $"PDV_DISCOVER_RESPONSE|{serverUrl}";
                        var responseBytes = Encoding.UTF8.GetBytes(responseMessage);

                        // Respond directly to the client's source IP and ephemeral port
                        await listener.SendAsync(responseBytes, responseBytes.Length, receiveResult.RemoteEndPoint);
                        _logger.LogInformation("Sent discovery response to: {ClientEndpoint}", receiveResult.RemoteEndPoint);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Normal shutdown
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in Server UDP Discovery Listener execution loop.");
                listener?.Dispose();
                listener = null;

                // Exponential backoff for binding retries to prevent tight loops on port conflicts
                var delaySeconds = Math.Min(30, (int)Math.Pow(2, retryCount));
                retryCount++;
                _logger.LogWarning("Retrying to bind Server UDP Discovery Listener in {Seconds} seconds...", delaySeconds);
                
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(delaySeconds), stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }

        listener?.Dispose();
        _logger.LogInformation("Server UDP Discovery Listener stopped.");
    }

    private string ResolveServerBaseUrl()
    {
        var configUrl = _configuration.GetValue<string>("SyncSettings:LocalServerBaseUrl");
        
        // If nothing configured, default to a standard port http://+:5000 or http://localhost:5000
        if (string.IsNullOrWhiteSpace(configUrl))
        {
            var defaultPort = _configuration.GetValue<string>("ASPNETCORE_URLS") ?? "http://+:5000";
            configUrl = defaultPort.Split(';').FirstOrDefault() ?? "http://+:5000";
        }

        var localIp = GetLocalIpAddress();
        
        // If configUrl has wildcards or loopback, replace with local IP
        if (configUrl.Contains("+") || configUrl.Contains("*") || configUrl.Contains("0.0.0.0") || configUrl.Contains("localhost") || configUrl.Contains("127.0.0.1"))
        {
            var uriBuilder = new UriBuilder(configUrl.Replace("+", "localhost").Replace("*", "localhost"));
            uriBuilder.Host = localIp;
            return uriBuilder.Uri.ToString().TrimEnd('/');
        }

        return configUrl.TrimEnd('/');
    }

    private string GetLocalIpAddress()
    {
        try
        {
            // Retrieve active physical interfaces to bypass virtual interfaces (VPN, VirtualBox, etc.)
            var interfaces = NetworkInterface.GetAllNetworkInterfaces()
                .Where(ni => ni.OperationalStatus == OperationalStatus.Up && 
                             ni.NetworkInterfaceType != NetworkInterfaceType.Loopback &&
                             !ni.Description.Contains("Virtual", StringComparison.OrdinalIgnoreCase) &&
                             !ni.Name.Contains("vEthernet", StringComparison.OrdinalIgnoreCase))
                .ToList();

            foreach (var ni in interfaces)
            {
                var ipProps = ni.GetIPProperties();
                foreach (var unicast in ipProps.UnicastAddresses)
                {
                    if (unicast.Address.AddressFamily == AddressFamily.InterNetwork)
                    {
                        return unicast.Address.ToString();
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to resolve IP via network interfaces. Falling back to DNS lookups.");
        }

        // Fallback to basic DNS hostname resolution
        try
        {
            var host = Dns.GetHostEntry(Dns.GetHostName());
            var ip = host.AddressList.FirstOrDefault(i => i.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(i));
            if (ip != null) return ip.ToString();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to resolve IP via DNS. Defaulting to 127.0.0.1.");
        }

        return "127.0.0.1";
    }
}
