using PDV.Application.Common.Interfaces;
using PDV.Infrastructure.Printing;
using PDV.HardwareAgent.Endpoints;

System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);

var builder = WebApplication.CreateBuilder(args);

// Configure the agent to listen on http://localhost:9000 for local requests
builder.WebHost.UseUrls("http://localhost:9000");

// Enable CORS to allow requests from any PWA frontend origin
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowPwa", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

// Register services (inversion of control using MultiChannelEscPosPrinter)
builder.Services.AddSingleton<IEscPosPrinter, MultiChannelEscPosPrinter>();

var app = builder.Build();

app.UseCors("AllowPwa");

// Map all printer and cash drawer endpoints cleanly
app.MapPrinterEndpoints();

app.Run();
