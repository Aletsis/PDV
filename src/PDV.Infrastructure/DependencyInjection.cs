using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PDV.Infrastructure.Identity;
using PDV.Infrastructure.Persistence;
using PDV.Infrastructure.Persistence.Interceptors;
using PDV.Infrastructure.Repositories;
using PDV.Infrastructure.Common;
using Microsoft.AspNetCore.Identity;
using PDV.Application.Common.Interfaces;
using PDV.Domain.Repositories;

namespace PDV.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddCommonInfrastructureServices(this IServiceCollection services)
    {
        // Registrar interceptor de eventos de dominio (Singleton: no tiene estado mutable)
        services.AddSingleton<DomainEventsInterceptor>(provider =>
        {
            var configuration = provider.GetRequiredService<Microsoft.Extensions.Configuration.IConfiguration>();
            var runMode = configuration["RunMode"] ?? "Server";
            bool isServerMode = string.Equals(runMode, "Server", System.StringComparison.OrdinalIgnoreCase);
            return new DomainEventsInterceptor(isServerMode);
        });

        services.AddScoped<IApplicationDbContext>(provider => provider.GetRequiredService<AppDbContext>());

        // Registrar repositorios
        services.AddScoped<IProductRepository, ProductRepository>();
        services.AddScoped<ISaleRepository, SaleRepository>();
        services.AddScoped<IBranchRepository, BranchRepository>();
        services.AddScoped<ISystemConfigurationRepository, SystemConfigurationRepository>();
        services.AddScoped<IFolioSequenceRepository, FolioSequenceRepository>();
        services.AddScoped<ITicketSequenceRepository, TicketSequenceRepository>();
        services.AddScoped<IOrderRepository, OrderRepository>();
        services.AddScoped<IDeliveryRouteRepository, DeliveryRouteRepository>();
        services.AddScoped<IDeliveryZoneRepository, DeliveryZoneRepository>();


        // Registrar servicios comunes
        services.AddScoped<IDateTimeService, DateTimeService>();
        services.AddScoped<ITicketGenerator, Printing.TicketGenerator>();
        services.AddScoped<IEscPosPrinter, Printing.MultiChannelEscPosPrinter>();
        services.AddScoped<IComercialApiSyncService, Common.ComercialApiSyncService>();
        services.AddScoped<ICsdCertificateService, CsdCertificateService>();
        services.AddScoped<ICfdiXmlGenerator, CfdiXmlGenerator>();
        services.AddScoped<IPacService, MockPacService>();
        services.AddScoped<IIdentityService, IdentityService>();
        services.AddScoped<IPermissionService, PermissionService>();
        services.AddScoped<ILocalStationService, Services.LocalStationService>();
        services.AddHttpClient<IGeocodingService, GeocodingService>();

        // Registrar Event Bus (in-memory de forma predeterminada)
        services.AddSingleton<IEventBus, Common.InMemoryEventBus>();

        // Configurar Caché Distribuido (Redis en modo Server, In-Memory en modo Local)
        var sp = services.BuildServiceProvider();
        var configuration = sp.GetRequiredService<Microsoft.Extensions.Configuration.IConfiguration>();
        var runMode = configuration["RunMode"] ?? "Server";
        
        if (string.Equals(runMode, "Server", System.StringComparison.OrdinalIgnoreCase))
        {
            var redisConnectionString = configuration.GetConnectionString("RedisConnection") ?? "localhost:6379";
            services.AddStackExchangeRedisCache(options =>
            {
                options.Configuration = redisConnectionString;
                options.InstanceName = "PDV_";
            });
        }
        else
        {
            services.AddDistributedMemoryCache();
        }

        services.AddScoped<ICacheService, Common.RedisCacheService>();

        services.AddIdentity<ApplicationUser, IdentityRole>(options =>
        {
            options.Password.RequireDigit = true;
            options.Password.RequireLowercase = true;
            options.Password.RequireNonAlphanumeric = true;
            options.Password.RequireUppercase = true;
            options.Password.RequiredLength = 8;
            options.User.RequireUniqueEmail = false;
        })
        .AddEntityFrameworkStores<AppDbContext>()
        .AddDefaultTokenProviders()
        .AddClaimsPrincipalFactory<Identity.AppUserClaimsPrincipalFactory>();

        // Configurar la ruta de redirección de cookies de autenticación para que coincida con nuestra vista personalizada (/login)
        services.ConfigureApplicationCookie(options =>
        {
            options.LoginPath = "/login";
            options.LogoutPath = "/logout";
            options.AccessDeniedPath = "/access-denied";
        });

        return services;
    }
}
