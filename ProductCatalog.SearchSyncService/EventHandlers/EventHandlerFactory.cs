using EventBus.Events;
using ProductCatalog.Events;

namespace ProductCatalog.SearchSyncService.EventHandlers;

internal interface IEventHandlerFactory
{
    IEventHandler? CreateHandler(IServiceProvider services, IntegrationEvent evt);
}

internal class EventHandlerFactory: IEventHandlerFactory
{
    public IEventHandler? CreateHandler(IServiceProvider services, IntegrationEvent evt)
    {
        return evt switch
        {
            ProductCreatedEvent => services.GetService<ProductCreatedEventHandler>(),
            _ => null
        };

    }
}
