using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PDV.Application.Common.Interfaces;
using PDV.Domain.Entities;
using PDV.Domain.Enums;

namespace PDV.Infrastructure.Server.BackgroundServices;

/// <summary>
/// Servicio en segundo plano que procesa la cola de integración CONTPAQi Comercial
/// de forma asíncrona y diferida para evitar demoras en la respuesta del punto de venta.
/// </summary>
public class ContpaqiSyncBackgroundWorker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ContpaqiSyncBackgroundWorker> _logger;

    public ContpaqiSyncBackgroundWorker(
        IServiceProvider serviceProvider,
        ILogger<ContpaqiSyncBackgroundWorker> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Iniciando ContpaqiSyncBackgroundWorker...");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessPendingTasksAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error durante el procesamiento de la cola de CONTPAQi.");
            }

            // Esperar 15 segundos antes de la siguiente iteración
            await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken);
        }

        _logger.LogInformation("ContpaqiSyncBackgroundWorker detenido.");
    }

    private async Task ProcessPendingTasksAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var comercialSyncService = scope.ServiceProvider.GetRequiredService<IComercialApiSyncService>();

        // Obtener tareas pendientes ordenadas por fecha de creación
        var pendingTasks = await context.ContpaqiSyncQueues
            .Where(t => t.State == OutboxState.Pending)
            .OrderBy(t => t.CreatedAt)
            .Take(20) // Procesar en lotes pequeños
            .ToListAsync(cancellationToken);

        if (pendingTasks.Count == 0) return;

        _logger.LogInformation("Procesando {Count} tareas pendientes en la cola de CONTPAQi...", pendingTasks.Count);

        foreach (var task in pendingTasks)
        {
            task.MarkAsProcessing();
            await context.SaveChangesAsync(cancellationToken);

            bool success = false;
            string? errorMessage = null;

            try
            {
                if (string.Equals(task.Type, "Client", StringComparison.OrdinalIgnoreCase))
                {
                    success = await ProcessClientSyncAsync(context, comercialSyncService, task, cancellationToken);
                }
                else if (string.Equals(task.Type, "Product", StringComparison.OrdinalIgnoreCase))
                {
                    success = await ProcessProductSyncAsync(context, comercialSyncService, task, cancellationToken);
                }
                else
                {
                    errorMessage = $"Tipo de entidad desconocido: '{task.Type}'";
                }
            }
            catch (Exception ex)
            {
                errorMessage = ex.Message;
                var inner = ex.InnerException;
                while (inner != null)
                {
                    errorMessage += " ---> " + inner.Message;
                    inner = inner.InnerException;
                }
            }

            if (success)
            {
                task.MarkAsProcessed();
                _logger.LogInformation("Tarea CONTPAQi {TaskId} ({Type}/{Action}) procesada con éxito.", task.Id, task.Type, task.Action);
            }
            else
            {
                errorMessage ??= "Fallo desconocido en la sincronización con CONTPAQi.";
                task.MarkAsFailed(errorMessage, maxAttempts: 5);
                _logger.LogWarning("Error al procesar tarea CONTPAQi {TaskId} ({Type}/{Action}). Error: {Error}. Intento: {Attempts}",
                    task.Id, task.Type, task.Action, errorMessage, task.Attempts);
            }

            await context.SaveChangesAsync(cancellationToken);
        }
    }

    private async Task<bool> ProcessClientSyncAsync(
        IApplicationDbContext context,
        IComercialApiSyncService comercialSyncService,
        ContpaqiSyncQueue task,
        CancellationToken cancellationToken)
    {
        Client? client = null;
        if (context is DbContext dbContext)
        {
            client = await dbContext.Set<Client>()
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(c => c.Id == task.ReferenceId, cancellationToken);
        }
        else
        {
            client = await context.Clients.FirstOrDefaultAsync(c => c.Id == task.ReferenceId, cancellationToken);
        }

        if (client == null)
        {
            _logger.LogWarning("Cliente {ClientId} no encontrado en base de datos. Saltando tarea.", task.ReferenceId);
            return true; // Marcar como procesado para no bloquear la cola
        }

        var exists = await comercialSyncService.ClientExistsInComercialAsync(client.Code, cancellationToken);
        if (!exists)
        {
            return await comercialSyncService.SendClientToComercialAsync(client, cancellationToken);
        }
        else
        {
            return await comercialSyncService.UpdateClientInComercialAsync(client, cancellationToken);
        }
    }

    private async Task<bool> ProcessProductSyncAsync(
        IApplicationDbContext context,
        IComercialApiSyncService comercialSyncService,
        ContpaqiSyncQueue task,
        CancellationToken cancellationToken)
    {
        Product? product = null;
        if (context is DbContext dbContext)
        {
            product = await dbContext.Set<Product>()
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(p => p.Id == task.ReferenceId, cancellationToken);
        }
        else
        {
            product = await context.Products.FirstOrDefaultAsync(p => p.Id == task.ReferenceId, cancellationToken);
        }

        if (product == null)
        {
            _logger.LogWarning("Producto {ProductId} no encontrado en base de datos. Saltando tarea.", task.ReferenceId);
            return true; // Marcar como procesado para no bloquear la cola
        }

        var exists = await comercialSyncService.ProductExistsInComercialAsync(product.Code, cancellationToken);
        if (!exists)
        {
            return await comercialSyncService.SendProductToComercialAsync(product, cancellationToken);
        }
        else
        {
            return await comercialSyncService.UpdateProductInComercialAsync(product, cancellationToken);
        }
    }
}
