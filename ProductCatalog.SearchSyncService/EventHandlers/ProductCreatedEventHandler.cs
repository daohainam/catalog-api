using EventBus.Events;
using ProductCatalog.Events;

namespace ProductCatalog.SearchSyncService.EventHandlers;
internal class ProductCreatedEventHandler(ILogger<ProductCreatedEventHandler> logger): IEventHandler
{
    public async Task HandleAsync(IntegrationEvent evt, CancellationToken cancellationToken)
    { 
        if (evt is not ProductCreatedEvent productCreatedEvent)
        {
            logger.LogError("Invalid event type: {t}", evt.GetType().FullName);
            return;
        }

        logger.LogInformation("Handled ProductCreatedEvent for ProductId: {ProductId}", productCreatedEvent.ProductId);
    }           
}