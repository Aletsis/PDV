using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using MudBlazor.Services;
using PDV.Application;
using PDV.Infrastructure;
using PDV.Infrastructure.Server;
using PDV.Infrastructure.Local;
using Microsoft.EntityFrameworkCore;
using Serilog;
using Serilog.Events;
using PDV.Infrastructure.Identity;
using PDV.WebUI.Components;
using PDV.Application.Common.Interfaces;
using PDV.Infrastructure.Printing;
using PDV.WebUI.Services;
using PDV.WebUI.Health;
using PDV.WebUI.Middleware;
using PDV.Infrastructure.Persistence;

System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);

var builder = WebApplication.CreateBuilder(args);

// Load user secrets in Development for sensitive configuration
if (builder.Environment.IsDevelopment())
{
    builder.Configuration.AddUserSecrets<Program>(optional: true);
}

// Configure Serilog early
var environmentName = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production";
var isProd = environmentName.Equals("Production", StringComparison.OrdinalIgnoreCase);

var loggerConfig = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
    .Enrich.FromLogContext();

if (isProd)
{
    loggerConfig.WriteTo.Console(new Serilog.Formatting.Json.JsonFormatter());
}
else
{
    loggerConfig.WriteTo.Console();
}

loggerConfig.WriteTo.File("Logs/pdv-webui-.log", rollingInterval: RollingInterval.Day, retainedFileCountLimit: 14);
Log.Logger = loggerConfig.CreateLogger();

builder.Host.UseSerilog();

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddRazorPages();
builder.Services.AddControllers();

// Permite a los componentes Blazor Server acceder al HttpContext (ej. para leer IP del cliente)
builder.Services.AddHttpContextAccessor();


// Health checks (include database check)
builder.Services.AddHealthChecks()
    .AddCheck<DatabaseHealthCheck>("database");

// Add Clean Architecture Layers
builder.Services.AddApplicationServices();

// Resolve connection string: prefer configuration, then environment variables
var defaultConnection = builder.Configuration.GetConnectionString("DefaultConnection")
                        ?? Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
                        ?? Environment.GetEnvironmentVariable("DEFAULT_CONNECTION");

if (builder.Environment.IsProduction() && string.IsNullOrWhiteSpace(defaultConnection))
{
    Log.Fatal("Connection string 'DefaultConnection' is not configured for Production. Aborting startup.");
    throw new InvalidOperationException("Connection string 'DefaultConnection' is required in Production environment.");
}

var runMode = builder.Configuration["RunMode"] ?? "Server";
if (runMode.Equals("Local", StringComparison.OrdinalIgnoreCase))
{
    builder.Services.AddLocalInfrastructureServices(defaultConnection!);
}
else
{
    builder.Services.AddServerInfrastructureServices(defaultConnection!);
}

// Register connection monitor for online/offline detection
builder.Services.AddSingleton<ConnectionMonitor>();

// Register ESC/POS printer implementation
builder.Services.AddScoped<IEscPosPrinter, MultiChannelEscPosPrinter>();

// Register UI-specific services (Blazor implementation)
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();

// Turno activo del cajero (por sesión Blazor Server)
builder.Services.AddScoped<PDV.WebUI.Services.ShiftSessionService>();

// Add MudBlazor services
builder.Services.AddMudServices();

// Add Authentication/Authorization
builder.Services.AddCascadingAuthenticationState();
// Use the built-in Identity components or custom ones. 
// For now, simpler setup without full UI generation, but we need the providers.
builder.Services.AddScoped<AuthenticationStateProvider, RevalidatingIdentityAuthenticationStateProvider<ApplicationUser>>();
builder.Services.AddSingleton<Microsoft.AspNetCore.Authorization.IAuthorizationHandler, PDV.WebUI.Services.AdminRolesAuthorizationHandler>();


var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

// Serilog request logging
app.UseSerilogRequestLogging();

// Global exception handling middleware (logs and returns generic error)
app.UseMiddleware<ExceptionHandlingMiddleware>();

app.UseHttpsRedirection();

app.UseStaticFiles();
app.UseAntiforgery();
app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.MapRazorPages();
app.MapControllers();

// Health checks
app.MapHealthChecks("/health/live");
app.MapHealthChecks("/health/ready");

// Optional: apply EF migrations at startup when env var APPLY_MIGRATIONS=true
var applyMigrations = Environment.GetEnvironmentVariable("APPLY_MIGRATIONS");
if (!string.IsNullOrWhiteSpace(applyMigrations) && applyMigrations.Equals("true", StringComparison.OrdinalIgnoreCase))
{
    try
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Database.Migrate();
        Log.Information("Applied pending migrations at startup.");
    }
    catch (Exception ex)
    {
        Log.Fatal(ex, "Failed applying migrations at startup");
        throw;
    }
}

// ── Seed de roles base ────────────────────────────────────────────────────
using (var seedScope = app.Services.CreateScope())
{
    var roleManager = seedScope.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.Identity.RoleManager<Microsoft.AspNetCore.Identity.IdentityRole>>();
    foreach (var roleName in new[] { "Admin", "Manager", "Cashier" })
    {
        if (!await roleManager.RoleExistsAsync(roleName))
        {
            await roleManager.CreateAsync(new Microsoft.AspNetCore.Identity.IdentityRole(roleName));
            Log.Information("Rol '{Role}' creado automáticamente.", roleName);
        }
    }
}

// Optional safety: abort startup if there are pending migrations and REQUIRE_NO_PENDING_MIGRATIONS=true
var requireNoPending = Environment.GetEnvironmentVariable("REQUIRE_NO_PENDING_MIGRATIONS");
if (!string.IsNullOrWhiteSpace(requireNoPending) && requireNoPending.Equals("true", StringComparison.OrdinalIgnoreCase))
{
    try
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var pending = db.Database.GetPendingMigrations();
        if (pending != null && pending.Any())
        {
            var list = string.Join(',', pending);
            Log.Fatal("Pending migrations detected: {Migrations}. Aborting startup due to REQUIRE_NO_PENDING_MIGRATIONS=true", list);
            throw new InvalidOperationException("Pending migrations detected: " + list);
        }
        Log.Information("No pending migrations detected.");
    }
    catch (Exception ex)
    {
        Log.Fatal(ex, "Failed to validate pending migrations at startup");
        throw;
    }
}

// Seed default database values (Roles, Admin user and linked Employee)
try
{
    using var scope = app.Services.CreateScope();
    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
    var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await PDV.Infrastructure.Persistence.AppDbContextSeed.SeedDefaultUserAsync(userManager, roleManager, context);
    Log.Information("Initial seed data checked and applied successfully.");
}
catch (Exception ex)
{
    Log.Error(ex, "An error occurred while seeding the database at startup.");
}

app.Run();
