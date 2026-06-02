using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using PDV.Application.Common.Interfaces;

namespace PDV.WebUI.Health;

public class DatabaseHealthCheck : IHealthCheck
{
    private readonly IApplicationDbContext _db;

    public DatabaseHealthCheck(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            // Try to perform a trivial query
            await _db.SaveChangesAsync(cancellationToken);
            return HealthCheckResult.Healthy("Database reachable");
        }
        catch (System.Exception ex)
        {
            return HealthCheckResult.Unhealthy("Database unreachable", ex);
        }
    }
}
