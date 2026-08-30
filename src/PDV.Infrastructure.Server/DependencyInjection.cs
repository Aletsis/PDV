using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PDV.Infrastructure.Persistence;
using PDV.Infrastructure.Persistence.Interceptors;
using PDV.Infrastructure.Server.Discovery;

namespace PDV.Infrastructure.Server;

public static class DependencyInjection
{
    public static IServiceCollection AddServerInfrastructureServices(this IServiceCollection services, string connectionString)
    {
        AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

        services.AddDbContext<AppDbContext>((serviceProvider, options) =>
        {
            var interceptor = serviceProvider.GetRequiredService<DomainEventsInterceptor>();
            options.UseNpgsql(
                connectionString,
                b => b.MigrationsAssembly(typeof(DependencyInjection).Assembly.FullName)
                      .UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery))
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning))
            .AddInterceptors(interceptor);
        });

        // Llamar a los servicios de infraestructura compartidos
        services.AddCommonInfrastructureServices();

        // Registrar el servicio de descubrimiento UDP en el servidor
        services.AddHostedService<ServerDiscoveryHostedService>();

        // Registrar el background worker de integración con CONTPAQi Comercial
        services.AddHostedService<PDV.Infrastructure.Server.BackgroundServices.ContpaqiSyncBackgroundWorker>();

        // Registrar el background worker de réplica / Inbox Pattern
        services.AddHostedService<PDV.Infrastructure.Server.BackgroundServices.InboxWorker>();

        return services;
    }
}
