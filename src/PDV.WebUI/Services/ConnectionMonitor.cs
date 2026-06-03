using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace PDV.WebUI.Services;

public class ConnectionMonitor : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly string _serverBaseUrl;
    private readonly bool _isLocalMode;
    private readonly ILogger<ConnectionMonitor> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly Timer _timer;
    
    private bool _isConnected = true;
    private bool _isChecking = false;

    public bool IsConnected => _isConnected;
    public bool IsLocalMode => _isLocalMode;

    public event Action<bool>? OnConnectionStatusChanged;

    public ConnectionMonitor(IConfiguration configuration, ILogger<ConnectionMonitor> logger, IServiceProvider serviceProvider)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
        _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };

        var runMode = configuration["RunMode"] ?? "Server";
        _isLocalMode = runMode.Equals("Local", StringComparison.OrdinalIgnoreCase);
        
        _serverBaseUrl = (configuration["SyncSettings:ServerBaseUrl"] ?? "").TrimEnd('/');

        if (_isLocalMode && !string.IsNullOrWhiteSpace(_serverBaseUrl))
        {
            // Start checking connection periodically only if running in Local mode
            _timer = new Timer(async _ => await CheckConnectionAsync(), null, TimeSpan.Zero, TimeSpan.FromSeconds(10));
            _logger.LogInformation("ConnectionMonitor initialized in Local mode. Target sync URL: {Url}", _serverBaseUrl);
        }
        else
        {
            // Server mode is always "connected" to itself
            _isConnected = true;
            _timer = new Timer(_ => { }, null, Timeout.Infinite, Timeout.Infinite);
            _logger.LogInformation("ConnectionMonitor initialized in Server mode. Network pinging is disabled.");
        }
    }

    public async Task<string?> GetResolvedServerUrlAsync()
    {
        if (!_isLocalMode) return null;

        var discoveryService = _serviceProvider.GetService<PDV.Infrastructure.Local.Discovery.IClientDiscoveryService>();
        if (discoveryService != null)
        {
            return await discoveryService.GetServerUrlAsync(CancellationToken.None);
        }

        return !string.IsNullOrWhiteSpace(_serverBaseUrl) &&
               !_serverBaseUrl.Equals("auto", StringComparison.OrdinalIgnoreCase) &&
               !_serverBaseUrl.Equals("discover", StringComparison.OrdinalIgnoreCase)
               ? _serverBaseUrl
               : null;
    }

    public async Task CheckConnectionAsync()
    {
        if (_isChecking) return;
        _isChecking = true;

        bool currentlyConnected = false;
        try
        {
            var baseUrl = await GetResolvedServerUrlAsync();
            if (!string.IsNullOrWhiteSpace(baseUrl))
            {
                var pingUrl = $"{baseUrl}/api/sync/ping";
                var response = await _httpClient.GetAsync(pingUrl);
                
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    currentlyConnected = content.Trim().Equals("pong", StringComparison.OrdinalIgnoreCase);
                }
            }
            else if (!_isLocalMode)
            {
                // In server mode we are always connected to self
                currentlyConnected = true;
            }
        }
        catch (Exception)
        {
            currentlyConnected = false;
        }
        finally
        {
            _isChecking = false;
        }


        if (currentlyConnected != _isConnected)
        {
            _isConnected = currentlyConnected;
            _logger.LogInformation("Network connectivity status changed. Connected to cloud server: {Status}", _isConnected);
            
            // Notify subscribers on the main thread/Blazor context
            OnConnectionStatusChanged?.Invoke(_isConnected);
        }
    }

    public void Dispose()
    {
        _timer?.Dispose();
        _httpClient?.Dispose();
    }
}
