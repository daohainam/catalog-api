using Elastic.Clients.Elasticsearch;
using EventBus.Events;
using ProductCatalog.Events;
using ProductCatalog.Search;

namespace ProductCatalog.SearchSyncService.EventHandlers;

internal class ProductDeletedEventHandler(ElasticsearchClient client, ILogger<ProductDeletedEventHandler> logger) : IEventHandler
{
    public async Task HandleAsync(IntegrationEvent evt, CancellationToken cancellationToken)
    {
        if (evt is not ProductDeletedEvent productDeletedEvent)
        {
            logger.LogError("Invalid event type: {t}", evt.GetType().FullName);
            return;
        }

        var response = await client.DeleteAsync<ProductIndexDocument>(
            productDeletedEvent.ProductId.ToString(), cancellationToken);

        // A document that is already gone is not an error - the delete is idempotent.
        if (!response.IsValidResponse && response.Result != Result.NotFound)
        {
            throw new InvalidOperationException(
                $"Elasticsearch rejected the delete for product {productDeletedEvent.ProductId}: {response.ElasticsearchServerError}");
        }

        logger.LogInformation("Removed product {ProductId} from the index", productDeletedEvent.ProductId);
    }
}
