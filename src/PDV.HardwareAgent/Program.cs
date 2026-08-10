using PDV.Application.Common.Interfaces;
using PDV.HardwareAgent.Endpoints;
using PDV.HardwareAgent.Profiles;
using PDV.HardwareAgent.Services;
using PDV.HardwareAgent.Transports;
using PDV.Infrastructure.Printing;

System.IO.Directory.SetCurrentDirectory(System.AppContext.BaseDirectory);
System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);

var builder = WebApplication.CreateBuilder(args);

// Configures the application lifetime to run as a Windows Service if started as such.
builder.Host.UseWindowsService();

// Configure the agent to listen on http://127.0.0.1:9000 for local requests (strictly loopback)
builder.WebHost.UseUrls("http://127.0.0.1:9000");

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

// Register Printer Modular Services
builder.Services.AddSingleton<ITransportFactory, TransportFactory>();
builder.Services.AddSingleton<IPrinterProfileFactory, PrinterProfileFactory>();
builder.Services.AddSingleton<IPrinterManager, PrinterManager>();

// Register Legacy Printer & Peripherals Services
builder.Services.AddSingleton<IEscPosPrinter, MultiChannelEscPosPrinter>();
builder.Services.AddSingleton<IScaleService, PDV.HardwareAgent.Services.ScaleService>();
builder.Services.AddSingleton<IPaymentTerminalService, PDV.HardwareAgent.Services.PaymentTerminalService>();

var app = builder.Build();

app.UseCors("AllowPwa");

// Map all endpoints
app.MapPrinterEndpoints();
app.MapScaleEndpoints();
app.MapPaymentEndpoints();

app.Run();
