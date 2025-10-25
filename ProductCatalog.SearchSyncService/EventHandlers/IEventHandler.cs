using EventBus.Events;

namespace ProductCatalog.SearchSyncService.EventHandlers;

internal interface IEventHandler
{
    Task HandleAsync(IntegrationEvent evt, CancellationToken cancellationToken);
}
