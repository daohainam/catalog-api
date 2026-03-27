using Elastic.Clients.Elasticsearch;
using EventBus.Events;
using ProductCatalog.Events;
using ProductCatalog.Search;

namespace ProductCatalog.SearchSyncService.EventHandlers;
internal class ProductCreatedEventHandler(ElasticsearchClient client, ILogger<ProductCreatedEventHandler> logger): IEventHandler
{
    public async Task HandleAsync(IntegrationEvent evt, CancellationToken cancellationToken)
    { 
        if (evt is not ProductCreatedEvent productCreatedEvent)
        {
            logger.LogError("Invalid event type: {t}", evt.GetType().FullName);
            return;
        }

        try
        {
            var doc = ProductEsMapper.Map(productCreatedEvent);

            logger.LogInformation("Indexing product {ProductId} to Elasticsearch with {VariantCount} variants",
                productCreatedEvent.ProductId, doc.Variants?.Count ?? 0);
            var response = await client.IndexAsync(doc, cancellationToken: cancellationToken);

            if (!response.IsValidResponse)
            {
                logger.LogWarning("Error indexing product {ProductId}: {Error}", productCreatedEvent.ProductId, response.ElasticsearchServerError);
            }
            else
            {
                logger.LogInformation("Successfully indexed product {ProductId}", productCreatedEvent.ProductId);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error indexing product {ProductId}", productCreatedEvent.ProductId);
            throw;
        }
    }           
}