using System.Threading;
using System.Threading.Tasks;

namespace PDV.Application.Common.Interfaces;

public interface IEventBus
{
    Task PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default)
        where TEvent : class;

    void Subscribe<TEvent, THandler>()
        where TEvent : class
        where THandler : IIntegrationEventHandler<TEvent>;
}

public interface IIntegrationEventHandler<in TEvent>
    where TEvent : class
{
    Task HandleAsync(TEvent @event, CancellationToken cancellationToken = default);
}
