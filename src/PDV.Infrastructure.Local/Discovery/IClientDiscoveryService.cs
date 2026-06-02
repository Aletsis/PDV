using System.Threading;
using System.Threading.Tasks;

namespace PDV.Infrastructure.Local.Discovery;

/// <summary>
/// Service responsible for resolving the central/branch server URL hierarchically.
/// Level 1: Static DNS / Configuration Hostname.
/// Level 2: Local network zero-conf discovery using UDP Broadcast.
/// </summary>
public interface IClientDiscoveryService
{
    /// <summary>
    /// Gets the resolved server base URL.
    /// </summary>
    Task<string?> GetServerUrlAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Invalidates the currently cached server URL so a new discovery process is triggered on the next request.
    /// Call this when a network communication attempt with the server fails.
    /// </summary>
    void InvalidateUrl();
}
