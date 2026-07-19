using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using PDV.Application.Common.Interfaces;

namespace PDV.Infrastructure.Common;

public class InMemoryEventBus : IEventBus
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ConcurrentDictionary<string, List<Type>> _handlers = new();

    public InMemoryEventBus(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public async Task PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default)
        where TEvent : class
    {
        var eventName = typeof(TEvent).Name;
        if (!_handlers.TryGetValue(eventName, out var handlerTypes))
        {
            return;
        }

        using var scope = _serviceProvider.CreateScope();
        foreach (var handlerType in handlerTypes)
        {
            var handler = scope.ServiceProvider.GetService(handlerType);
            if (handler == null) continue;

            var method = handlerType.GetMethod("HandleAsync");
            if (method != null)
            {
                var task = (Task?)method.Invoke(handler, new object[] { @event, cancellationToken });
                if (task != null)
                {
                    await task;
                }
            }
        }
    }

    public void Subscribe<TEvent, THandler>()
        where TEvent : class
        where THandler : IIntegrationEventHandler<TEvent>
    {
        var eventName = typeof(TEvent).Name;
        var handlerType = typeof(THandler);

        _handlers.AddOrUpdate(
            eventName,
            _ => new List<Type> { handlerType },
            (_, existing) =>
            {
                if (!existing.Contains(handlerType))
                {
                    existing.Add(handlerType);
                }
                return existing;
            });
    }
}
