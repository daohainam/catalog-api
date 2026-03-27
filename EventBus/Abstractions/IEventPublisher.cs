using EventBus.Events;

namespace EventBus.Abstractions;
public interface IEventPublisher
{
    Task<bool> PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default) where TEvent : IntegrationEvent;
}
