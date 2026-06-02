using Microsoft.EntityFrameworkCore;
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
        services.AddSingleton<DomainEventsInterceptor>();

        services.AddScoped<IApplicationDbContext>(provider => provider.GetRequiredService<AppDbContext>());

        // Registrar repositorios
        services.AddScoped<IProductRepository, ProductRepository>();
        services.AddScoped<ISaleRepository, SaleRepository>();
        services.AddScoped<IBranchRepository, BranchRepository>();
        services.AddScoped<ISystemConfigurationRepository, SystemConfigurationRepository>();
        services.AddScoped<IFolioSequenceRepository, FolioSequenceRepository>();
        services.AddScoped<ITicketSequenceRepository, TicketSequenceRepository>();


        // Registrar servicios comunes
        services.AddScoped<IDateTimeService, DateTimeService>();
        services.AddScoped<ITicketGenerator, Printing.TicketGenerator>();
        services.AddScoped<IEscPosPrinter, Printing.MultiChannelEscPosPrinter>();
        services.AddScoped<IComercialApiSyncService, Common.ComercialApiSyncService>();

        services.AddIdentity<ApplicationUser, IdentityRole>(options =>
        {
            options.Password.RequireDigit = false;
            options.Password.RequireLowercase = false;
            options.Password.RequireNonAlphanumeric = false;
            options.Password.RequireUppercase = false;
            options.Password.RequiredLength = 4;
            options.User.RequireUniqueEmail = false; 
        })
        .AddEntityFrameworkStores<AppDbContext>()
        .AddDefaultTokenProviders();

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
