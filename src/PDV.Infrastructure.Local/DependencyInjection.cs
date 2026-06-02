using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PDV.Infrastructure.Local.BackgroundServices;
using PDV.Infrastructure.Local.Discovery;
using PDV.Infrastructure.Persistence;
using PDV.Infrastructure.Persistence.Interceptors;

namespace PDV.Infrastructure.Local;

public static class DependencyInjection
{
    public static IServiceCollection AddLocalInfrastructureServices(this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<AppDbContext>((serviceProvider, options) =>
        {
            var interceptor = serviceProvider.GetRequiredService<DomainEventsInterceptor>();
            options.UseSqlite(
                connectionString,
                b => b.MigrationsAssembly(typeof(DependencyInjection).Assembly.FullName)
                      .UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery))
            .AddInterceptors(interceptor);
        });

        // Llamar a los servicios de infraestructura compartidos
        services.AddCommonInfrastructureServices();

        // Registrar el servicio de descubrimiento jerárquico del servidor
        services.AddSingleton<IClientDiscoveryService, ClientDiscoveryService>();

        // Registrar el background worker de sincronización offline
        services.AddHostedService<SyncWorker>();

        return services;
    }
}

