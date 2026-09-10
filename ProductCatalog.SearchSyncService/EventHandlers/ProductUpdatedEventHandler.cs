using EventBus.Events;
using ProductCatalog.Events;
using ProductCatalog.Search;

namespace ProductCatalog.SearchSyncService.EventHandlers;

internal class ProductUpdatedEventHandler(ProductIndexWriter indexWriter, ILogger<ProductUpdatedEventHandler> logger) : IEventHandler
{
    public async Task HandleAsync(IntegrationEvent evt, CancellationToken cancellationToken)
    {
        if (evt is not ProductUpdatedEvent productUpdatedEvent)
        {
            logger.LogError("Invalid event type: {t}", evt.GetType().FullName);
            return;
        }

        await indexWriter.IndexAsync(ProductEsMapper.Map(productUpdatedEvent), cancellationToken);
    }
}
